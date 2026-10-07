# SmartGarage

Informacioni sistem za digitalizaciju zakazivanja i upravljanja servisnim uslugama za motorna vozila.

**Student:** Salih Nurković, **IB220014**
**Predmet:** Razvoj softvera II — Fakultet informacijskih tehnologija, Univerzitet „Džemal Bijedić" u Mostaru

> **Status:** Faza 1 od 15 (struktura rješenja i setup projekta). Ovaj README se dopunjava sa svakom fazom.

---

## Sadržaj

- [Arhitektura](#arhitektura)
- [Preduslovi](#preduslovi)
- [Konfiguracija](#konfiguracija)
- [Pokretanje kroz Docker](#pokretanje-kroz-docker)
- [Pokretanje iz Visual Studia](#pokretanje-iz-visual-studia)
- [Baza podataka](#baza-podataka)
- [Mobilna aplikacija](#mobilna-aplikacija)
- [Desktop aplikacija](#desktop-aplikacija)
- [Test korisnici](#test-korisnici)
- [Uloge](#uloge)
- [Stripe](#stripe)

---

## Arhitektura

Sistem čine četiri komponente: REST API, zaseban Worker mikroservis, mobilna i desktop Flutter aplikacija.

```
   Flutter Mobile (klijenti)          Flutter Windows (admin / zaposlenici)
              │                                      │
              └──────────── HTTP + SignalR ──────────┘
                                 │
                        SmartGarage.API
            Controller → Service → DbContext
                     │                    │
              RabbitMQ                SQL Server
                     │                    │
                SmartGarage.Worker ───────┘
            (e-mail, notifikacije, zakazani poslovi)
```

**Worker je zaseban projekat, zaseban Dockerfile, zaseban kontejner i zaseban proces.**
Nije `BackgroundService` unutar API-ja, što upute (sekcija 3.2) izričito ne priznaju kao mikroservis.

### Struktura rješenja

| Projekat | Uloga |
|---|---|
| `SmartGarage.Domain` | Entiteti, enumi, konstante, state machine, domenski izuzeci. Bez ijedne vanjske zavisnosti. |
| `SmartGarage.Contracts` | Poruke koje dijele API i Worker. Leaf projekat — ne referencira ništa. |
| `SmartGarage.Application` | Servisni sloj, DTO objekti, validatori, sistem preporuka. |
| `SmartGarage.Infrastructure` | `DbContext`, EF konfiguracije, migracije, seeder, Stripe, SMTP, RabbitMQ, PDF. |
| `SmartGarage.API` | Kontroleri, middleware, SignalR hub, DI kompozicija. |
| `SmartGarage.Worker` | RabbitMQ consumeri i Quartz zakazani poslovi. |
| `SmartGarage.UnitTests` | State machine, preporuke, validatori, obračun cijena. |
| `SmartGarage.IntegrationTests` | Autentifikacija, CRUD, konkurentnost rezervacija, idempotentnost plaćanja. |

Kontroleri referenciraju **isključivo** interfejse iz `Application` sloja. `DbContext` im nije ni u vidnom polju, pa pravilo „kontroler → servis → DbContext" nije stvar discipline nego strukture.

---

## Preduslovi

| Alat | Verzija |
|---|---|
| .NET SDK | 9.0 |
| Visual Studio 2026 ili VS Code | — |
| Flutter SDK | posljednja stabilna |
| Docker Desktop | posljednja stabilna |
| SQL Server | dolazi kroz Docker |

---

## Konfiguracija

Sve konfiguracijske vrijednosti žive u **jednoj** `.env` datoteci u korijenu repozitorija.
`appsettings.json` ne sadrži nijednu tajnu niti vrijednost specifičnu za okruženje.

```bash
cp .env.example .env
```

Zatim u `.env` popuni:

| Ključ | Napomena |
|---|---|
| `JWT_KEY` | Minimalno 32 bajta. Generiši novi. |
| `STRIPE_SECRET_KEY` | `sk_test_...` sa Stripe Sandbox-a |
| `STRIPE_PUBLISHABLE_KEY` | `pk_test_...` |
| `STRIPE_WEBHOOK_SECRET` | `whsec_...`, vidi [Stripe](#stripe) |
| `MSSQL_SA_PASSWORD` | Lozinka za SQL Server kontejner |

`.env` je u `.gitignore` i **ne postavlja se u GitHub Release**. Za predaju se umjesto njega prilaže `.env-tajne.zip` zaštićen šifrom, a šifra se predaje na DL sistem.

### Zašto nema HTTPS-a

API namjerno radi preko običnog HTTP-a. Upute (sekcija 9.2) to preporučuju jer self-signed certifikati mogu isteći ili biti nevalidni u trenutku pregleda rada. `UseHttpsRedirection()` se nigdje ne poziva.

---

## Pokretanje kroz Docker

> Dolazi u Fazi 14. Tada će jedna komanda dizati kompletan sistem:

```bash
docker compose up --build -d
```

Servisi: `sqlserver`, `rabbitmq`, `mailhog`, `api`, `worker` — svi na istoj mreži, sa healthcheck-ovima i eksplicitno verzionisanim image tagovima.

---

## Pokretanje iz Visual Studia

```bash
dotnet restore
dotnet build
```

Zatim postavi **oba** projekta kao startup (desni klik na rješenje → *Configure Startup Projects* → *Multiple startup projects*):

| Projekat | Akcija |
|---|---|
| `SmartGarage.API` | Start |
| `SmartGarage.Worker` | Start |

API sluša na `http://localhost:5000`, Swagger na `http://localhost:5000/swagger`.

Provjera da host radi:

```bash
curl http://localhost:5000/health
```

---

## Baza podataka

Baza se zove **`220014`** — broj indeksa bez prefiksa `IB`, kako traže upute (sekcija 3.1).

Pristup je Code First. Migracije se primjenjuju automatski pri pokretanju API-ja; Worker čeka da API postane zdrav, pa migracije nikada ne idu paralelno.

```bash
# Ručno, ako zatreba (dolazi u Fazi 2):
dotnet ef database update --project backend/src/SmartGarage.Infrastructure --startup-project backend/src/SmartGarage.API
```

---

## Mobilna aplikacija

> Dolazi u Fazi 10.

Adresa API-ja za Android emulator je `10.0.2.2`:

```bash
cd mobile/smartgarage_mobile
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5000
```

Release build:

```bash
flutter clean
flutter build apk --release
```

---

## Desktop aplikacija

> Dolazi u Fazi 11.

Adresa API-ja za Windows je `localhost`:

```bash
cd desktop/smartgarage_desktop
flutter run -d windows --dart-define=API_BASE_URL=http://localhost:5000
```

Release build:

```bash
flutter clean
flutter build windows --release
```

---

## Test korisnici

> Nalozi se kreiraju seederom u Fazi 2. Prijava ide **korisničkim imenom**, ne e-mailom.

| Kontekst | Korisničko ime | Lozinka |
|---|---|---|
| Desktop verzija | `desktop` | `test` |
| Mobilna verzija | `mobile` | `test` |
| Administrator | `admin` | `test` |
| Zaposlenik | `employee` | `test` |
| Klijent | `customer` | `test` |

Dodatni nalozi za demonstraciju sistema preporuka:

| Korisničko ime | Lozinka | Scenarij |
|---|---|---|
| `amar` | `test` | Bogata historija servisa — dizel 2018, 182 000 km |
| `lejla` | `test` | Bez historije — benzin 2023, 12 000 km (cold start) |
| `nedim` | `test` | Staro vozilo sa velikom kilometražom — dizel 2012, 310 000 km |
| `ivana` | `test` | Ima završen neplaćen servis (demonstracija plaćanja) |
| `neaktivan` | `test` | Deaktiviran nalog — prijava je onemogućena |

---

## Uloge

| Uloga | Pristup |
|---|---|
| **Administrator** | Sve: korisnici, zaposlenici, usluge, referentni podaci, rezervacije, povrati sredstava, izvještaji, log grešaka |
| **Employee** | Desktop: rezervacije (potvrda, pokretanje, završetak, otkazivanje), vlastiti profil. Bez upravljanja korisnicima, bez povrata, bez finansijskih izvještaja |
| **Customer** | Mobilna: vlastita vozila, zakazivanje, plaćanje, recenzije, notifikacije, preporuke |

Uloga se **nikada** ne prima sa klijenta — `RegisterRequest` nema polje za ulogu, pa je ni zlonamjeran klijent ne može postaviti. Zaposlenike i administratore kreira administrator kroz desktop aplikaciju.

---

## Stripe

Plaćanje koristi **stvarni Stripe Sandbox**, ništa nije simulirano. Iznos određuje server iz snimljenih cijena stavki; klijent šalje samo `appointmentId`.

Stripe ne može dosegnuti `localhost`, pa se za webhook-e koristi Stripe CLI:

```bash
stripe login
stripe listen --forward-to http://localhost:5000/api/webhooks/stripe
```

Komanda ispisuje `whsec_...` vrijednost koja ide u `STRIPE_WEBHOOK_SECRET`.

Ako CLI nije pokrenut, plaćanje i dalje radi ispravno: `PaymentReconciliationJob` u Worker-u svakih 15 minuta provjerava zaglavljena plaćanja direktno kod Stripe-a, a `GET /api/payments/{id}/status` radi istu provjeru na zahtjev. Potvrda plaćanja je u svakom slučaju serverska.

Test kartice:

| Broj | Ishod |
|---|---|
| `4242 4242 4242 4242` | Uspješno |
| `4000 0000 0000 0002` | Odbijeno |
| `4000 0025 0000 3155` | Zahtijeva 3D Secure |

---

## Dokumentacija

| Datoteka | Sadržaj |
|---|---|
| `docs/SmartGarage-Architecture-Plan.md` | Kompletna tehnička arhitektura i plan implementacije |
| `docs/SmartGarage-Revision-1.1-Uskladjivanje.md` | Usklađivanje sa prihvaćenom prijavom i zvaničnim uputama |
| `recommender-dokumentacija.md` | Dokumentacija sistema preporuka (Faza 9) |
