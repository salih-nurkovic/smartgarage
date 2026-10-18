# SmartGarage

Informacioni sistem za digitalizaciju zakazivanja i upravljanja servisnim uslugama za motorna vozila.

**Student:** Salih Nurković, **IB220014**
**Predmet:** Razvoj softvera II — Fakultet informacijskih tehnologija, Univerzitet „Džemal Bijedić" u Mostaru

> **Status:** REST API, Worker, sistem preporuka i mobilna aplikacija su završeni. Desktop aplikacija, PDF izvještaji i kompletan Docker setup su u izradi.

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

### Valuta

Cijene se čuvaju, prikazuju i naplaćuju u **EUR**, pa je iznos koji kupac vidi tačno iznos koji Stripe naplati. Mockup-i u prijavi prikazuju KM, ali Stripe ne naplaćuje u BAM. Oznaka valute uz cijene dolazi iz `DISPLAY_CURRENCY`.

Naziv, adresa i telefon servisa (`GARAGE_NAME`, `GARAGE_ADDRESS`, `GARAGE_PHONE`) prikazuju se u detaljima rezervacije.

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
# Ručno, ako zatreba:
dotnet ef database update --project backend/src/SmartGarage.Infrastructure --startup-project backend/src/SmartGarage.API
```

---

## Mobilna aplikacija

Flutter aplikacija za kupce (Android), u folderu `mobile/smartgarage_mobile`. Prijava nalogom `mobile` / `test`.

Adresa API-ja čita se preko `String.fromEnvironment('API_BASE_URL')`. Podrazumijevana vrijednost je `http://10.0.2.2:5000`, adresa računara viđena iz Android emulatora, pa APK radi i bez dodatnih parametara:

```bash
cd mobile/smartgarage_mobile
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5000
```

Release build:

```bash
flutter clean
flutter build apk --release
```

### Šta aplikacija pokriva

| Dio | Ekrani |
|---|---|
| Nalog | Prijava, registracija (uvijek kao kupac), reset lozinke kodom iz e-maila, profil sa slikom, promjena lozinke, odjava (sesija se poništava i na serveru) |
| Vozila | Lista sa kilometražom, zadnjim i sljedećim servisom; dodavanje i uređivanje (marka → model iz baze); brisanje vozila bez rezervacija |
| Usluge | Katalog sa pretragom, filterima (kategorija, cijena, trajanje) i sortiranjem; detalji usluge sa ocjenama |
| Zakazivanje | Četiri koraka kao u prijavi: vozilo → usluge → datum i termin (puni sati 08:00–16:00, slobodna mjesta sa servera) → potvrda |
| Rezervacije | **Master-detail forma**: historija sa brojačima i filterom statusa → detalji (usluge, termin, mehaničar, plaćanje, historija statusa); otkazivanje sa razlogom i promjena termina |
| Plaćanje | Stripe PaymentSheet unutar aplikacije, nakon završenog servisa; status „Plaćeno" postavlja isključivo server; pregled plaćanja i povrata |
| Recenzije | Prosjek i raspodjela 5★–1★, nova recenzija za završen servis, brisanje vlastite |
| Obavijesti | Stižu uživo preko SignalR-a (uz periodičnu provjeru kad veza nije dostupna); označi pročitano, označi sve, obriši |
| Preporuke | Po vozilu, sa hitnošću i objašnjenjem; „Zakaži" i „Obavljeno" |

### Odstupanje od mockup-a

Mockup `mobile-11` prikazuje vlastitu formu za broj kartice. Upute (7.1) traže in-app Stripe SDK / PaymentSheet, a vlastita forma bi aplikaciju uvela u PCI opseg, pa se koristi Stripe PaymentSheet. Donja navigacija ima pet tabova (Početna · Servisi · Zakaži · Historija · Profil), a plaćanja i recenzije se otvaraju iz detalja rezervacije i profila.

### Testovi

```bash
flutter analyze
flutter test
```

Ugovorni test poziva pokrenut API sa seed podacima kroz iste repozitorije koje koristi aplikacija. Mijenja podatke (kreira i otkazuje rezervaciju, dodaje i briše vozilo), pa se pokreće samo izričito:

```bash
flutter test test/e2e --dart-define=E2E=true --dart-define=API_BASE_URL=http://localhost:5000
```

---

## Desktop aplikacija

Flutter Windows aplikacija za osoblje, u folderu `desktop/smartgarage_desktop`. Prijava nalogom `desktop` / `test` (ili `admin`, odnosno `employee` za zaposlenika). Kupci se ne mogu prijaviti.

Podrazumijevana adresa API-ja je `http://localhost:5000`:

```bash
cd desktop/smartgarage_desktop
flutter run -d windows --dart-define=API_BASE_URL=http://localhost:5000
```

Release build:

```bash
flutter clean
flutter build windows --release
```

Za build sa pluginima Windows traži uključen **Developer Mode** (Postavke → Za programere).

| Ekran | Administrator | Zaposlenik |
|---|---|---|
| Dashboard: KPI sa promjenom u odnosu na prošli mjesec, prihod po mjesecima, raspodjela usluga, nadolazeće rezervacije | ✓ | ✓ bez prihoda |
| Rezervacije: brojači, pretraga, filter statusa i perioda, detalji sa potvrdom, dodjelom mehaničara, pomjeranjem, početkom, završetkom i otkazivanjem uz razlog | ✓ | ✓ bez dodjele |
| Usluge: kartice, dodavanje i uređivanje sa slikom, oznakama i tipovima motora | ✓ | |
| Zaposlenici: kartice, dodavanje, uređivanje, radno vrijeme, deaktivacija | ✓ | |
| Korisnici: pretraga, uloge, aktivacija i deaktivacija uz razlog, nova lozinka bez stare | ✓ | |
| Plaćanja: prihod, transakcije, povrat novca do stvarno naplaćenog iznosa | ✓ | |
| Recenzije: sakrivanje i ponovno prikazivanje | ✓ | |
| Šifarnici: kategorije, oznake, marke, modeli, tipovi motora | ✓ | |
| Log grešaka sa stack traceom | ✓ | |
| Obavijesti uživo, moj profil i promjena lozinke | ✓ | ✓ |

Nedostupne akcije ostaju vidljive, ali onemogućene, uz objašnjenje zašto (npr. brisanje usluge koja je na rezervacijama). Rezervacije i plaćanja prikazuju broj (`APP-…`, `PAY-…`), nikad ID iz baze.

Testovi su isti kao kod mobilne aplikacije: `flutter analyze`, `flutter test` i ugovorni test uz `--dart-define=E2E=true`.

---

## Test korisnici

> Nalozi se kreiraju seederom pri prvom pokretanju API-ja. Prijava ide **korisničkim imenom**, ne e-mailom.

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

Ako CLI nije pokrenut, plaćanje i dalje radi ispravno: mobilna aplikacija nakon PaymentSheet-a poziva `GET /api/payments/{id}`, koji status provjerava direktno kod Stripe-a, a `PaymentReconciliationJob` u Worker-u svakih 15 minuta provjerava zaglavljena plaćanja. Potvrda plaćanja je u svakom slučaju serverska.

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
| `recommender-dokumentacija.md` | Sistem preporuka: ulazni signali, vektori, bodovanje, objašnjenja, konfiguracija |
