# SmartGarage — sistem preporuka

Dokument opisuje sistem preporuka servisnih usluga onako kako je implementiran. Sve formule,
težine i pragovi odgovaraju kodu i vrijednostima iz `.env`.

| | |
|---|---|
| Algoritam | **Content-based filtering** (filtriranje po sadržaju), verzija `cbf-1.0` |
| Šta se preporučuje | Servisne usluge iz kataloga, **za konkretno vozilo** kupca |
| Gdje se prikazuje | Mobilna aplikacija, ekran „Preporučeni servisi" |
| Endpoint | `GET /api/recommendations?vehicleId={id}` (samo uloga Customer) |
| Kod | `SmartGarage.Application/Recommender/RecommenderEngine.cs` (algoritam), `Services/RecommendationService.cs` (podaci), `Controllers/RecommendationsController.cs` |

---

## 1. Zašto content-based

Servis za vozila ima malo kupaca, a svaki kupac ima malo vozila i malo servisa godišnje. Ocjene
su rijetke. Kolaborativno filtriranje („kupci slični vama su uzeli…") traži gustu matricu
korisnik × usluga, koje ovdje nema. Novi kupac bi ostao bez preporuka.

Content-based pristup opisuje **usluge** i **vozilo** istim osobinama: kategorija, oznake (tagovi)
i tip motora. Zato radi i za kupca bez ijednog servisa, jer o vozilu se uvijek zna motor, starost
i kilometraža. Historija kupca zatim postepeno pomjera preporuke prema onome što kupac stvarno
koristi.

Dva elementa iz domene se dodaju kao zasebni faktori jer ih sama sličnost ne hvata: **dospjelost
po intervalu** (km i mjeseci) i **šta se najčešće radi na istoj marki i modelu**.

---

## 2. Ulazni podaci

### 2.1. Vozilo

Tip motora, godina proizvodnje (iz nje starost u godinama), trenutna kilometraža, marka i model.
Vozilo mora pripadati prijavljenom kupcu. `userId` se uzima iz JWT tokena, a za tuđe vozilo
odgovor je `400`.

### 2.2. Katalog usluga

Za svaku aktivnu uslugu u aktivnoj kategoriji:

- kategorija;
- oznake (`ServiceTag`), svaka sa težinom veze (`ServiceTagLink.Weight`);
- tipovi motora za koje je usluga namijenjena (`ServiceEngineTypeLink`). Prazna lista znači
  „svi motori";
- preporučeni interval u km i/ili mjesecima;
- raspon starosti vozila (min/max godina), ako je zadan;
- prosječna ocjena i broj vidljivih recenzija.

Katalog se učitava jednim upitom i drži u memoriji 10 minuta. Svaka izmjena kataloga ili
referentnih tabela briše keš.

### 2.3. Signali ponašanja (`UserServiceSignals`)

Svaka radnja kupca nad uslugom upisuje signal sa težinom. Pozitivna težina znači interes,
negativna odbijanje.

| Signal | Kada nastaje | Težina |
|---|---|---|
| `Viewed` | Kupac otvori detalje usluge (`GET /api/services/{id}`) | **0,5** |
| `Searched` | Kupac pretraži katalog; signal dobijaju prva 3 rezultata | **0,3** |
| `RecommendationClicked` | Kupac klikne „Rezerviši" na preporuci | **1,0** |
| `Booked` | Rezervacija je napravljena (svaka usluga na njoj) | **2,0** |
| `MarkedDone` | Kupac označi „Već urađeno" | **1,5** |
| `Completed` | Servis je završen (status `Completed`) | **3,0** |
| `Reviewed` (4–5★) | Pozitivna recenzija | **2,5** |
| `Reviewed` (1–3★) | Negativna recenzija | **−1,5** |
| `Cancelled` | Rezervacija je otkazana | **−1,0** |

Signale osoblja i administratora sistem ne bilježi. Bilježi samo kupce, jer samo kupcima
preporučuje.

**Svaki prikupljeni signal ulazi u bodovanje:**

| Signal | Kako se koristi |
|---|---|
| Svi signali | Profil vozila (sekcija 4), sa težinom iz tabele i vremenskim opadanjem |
| `Completed` i `MarkedDone` | Određuju **datum i kilometražu zadnjeg obavljanja**, iz kojih se računa dospjelost (sekcija 5.2) |
| `Completed` svih kupaca | Statistika marke i modela (sekcija 5.5) |
| Recenzije | Prosječna ocjena usluge (sekcija 5.4) |

Za profil se koriste signali kupca vezani za to vozilo i signali bez vozila (pregled i pretraga
kataloga).

---

## 3. Vektorska reprezentacija usluge

Svaka usluga je **rijetki vektor** (sparse vector). Čuvaju se samo komponente različite od nule:

| Ključ | Vrijednost |
|---|---|
| `c:{kategorija}` | 1 |
| `t:{oznaka}` | težina veze × IDF oznake |
| `e:{tip motora}` | 1, za svaki navedeni tip motora |

Vektor se normalizuje na dužinu 1, pa je skalarni proizvod dva vektora kosinusna sličnost.

### 3.1. IDF težine oznaka

Oznaka koju ima većina usluga malo govori o bilo kojoj od njih. Rijetka oznaka govori mnogo.
Zato se oznake ponderišu inverznom frekvencijom (IDF):

```
idf(t) = max(0,1;  ln( N / (1 + df(t)) ))
```

gdje je `N` broj usluga u katalogu, a `df(t)` broj usluga koje imaju oznaku `t`. Donja granica
0,1 sprečava da česta oznaka potpuno nestane.

---

## 4. Profil vozila

Profil je vektor u istom prostoru kao usluge. Gradi se iz dva dijela.

### 4.1. Atributi vozila (uvijek postoje)

- `e:{tip motora vozila}` = 1;
- oznaka tipa motora (npr. `t:diesel`) sa svojim IDF-om;
- `t:high-mileage` ako vozilo ima više od **150 000 km** (`HIGH_MILEAGE_THRESHOLD_KM`);
- `t:older-vehicle` ako je vozilo starije od **10 godina** (`OLD_VEHICLE_AGE_YEARS`).

Vektor atributa se normalizuje.

### 4.2. Historija kupca

Zbir vektora usluga iz signala, svaki pomnožen težinom signala i faktorom opadanja:

```
historija = Σ  težina(s) · 0,5^(starost_dana(s) / 180) · vektor(usluga(s))
```

Signal star **180 dana** (`SIGNAL_HALF_LIFE_DAYS`) vrijedi upola manje od svježeg. Negativni
signali oduzimaju, pa otkazana usluga i loše ocijenjena usluga smanjuju sličnost sa sobom i
sebi sličnim uslugama.

### 4.3. Spajanje

Koliko historija utiče zavisi od toga koliko je ima:

```
dokaz  = Σ 0,5^(starost_dana(s) / 180)            (broj signala, umanjen za starost)
α      = min(1;  dokaz / 8)                       (SIGNAL_SATURATION_COUNT = 8)
β      = 0,7                                      (HISTORY_BLEND_BETA)

profil = norm( (1 − α + α·(1 − β)) · atributi  +  α·β · norm(historija) )
```

- **Novi kupac** (nema signala): α = 0, profil čine samo atributi vozila. Preporuke postoje od
  prvog dana (rješenje cold-start problema).
- **Kupac sa bogatom historijom** (α = 1): profil je 70 % historija, 30 % vozilo. Vozilo nikad
  ne nestane iz profila, pa dizel vozilo ne „zaboravi" da je dizel.
- **Stara historija** daje manji α nego ista količina svježe. Kupac koji dugo nije bio aktivan
  postepeno se vraća na profil vozila.

---

## 5. Bodovanje

Za svaku uslugu koja prođe filtere (sekcija 6) računa se skor kao **zbir faktora**. Svaki
faktor ima šifru, tekst za kupca i doprinos. Težine su u `.env`.

| Faktor | Šifra | Doprinos |
|---|---|---|
| Sličnost profila | `SIMILARITY` / `HISTORY_CATEGORY` | `max(0; profil · vektor_usluge)`, od 0 do 1 |
| Dospjelost po km | `MILEAGE_DUE` | `0,30 · min(1; omjer)` (`W_MILEAGE`) |
| Dospjelost po vremenu | `TIME_DUE` | `0,30 · min(1; omjer)` |
| Motor | `ENGINE_MATCH` / `ENGINE_ANY` | `0,20` za uslugu namijenjenu ovom motoru, `0,10` za uslugu za sve motore (`W_ENGINE`) |
| Starost vozila | `AGE_FIT` | `0,10` ako je starost u rasponu usluge (`W_AGE`) |
| Ocjena | `RATING` | `0,10 · (prosjek − 3) / 2`, od −0,10 do +0,10 (`W_RATING`) |
| Marka i model | `BRAND_MODEL` | `0,15 · max(udio_model; 0,6 · udio_marka)` (`W_BRAND_MODEL`) |
| Nedavno urađeno | `RECENTLY_DONE` | `−0,50`, kazna (`W_RECENT`) |

### 5.1. Sličnost

Kosinusna sličnost profila i vektora usluge. Ako je kupac već koristio usluge iz te kategorije,
faktor se zove `HISTORY_CATEGORY` i obrazloženje glasi *„već ste koristili usluge iz kategorije
„Kočnice""*. Inače je `SIMILARITY`: *„odgovara profilu vašeg vozila (motor, starost i
kilometraža)"*.

### 5.2. Dospjelost

Za uslugu sa intervalom računa se koliki je dio intervala prošao:

- **po km**: `(trenutna km − km pri zadnjem obavljanju) / interval_km`;
- **po vremenu**: `mjeseci od zadnjeg obavljanja / interval_mjeseci`.

Uzima se **veći** od ta dva omjera. Omjer 1 znači tačno dospjelo, a doprinos raste do 1 i tu
staje.

Zadnje obavljanje je kasniji od ova dva događaja: završena rezervacija koja je sadržavala tu
uslugu (km iz `MileageAtAppointmentKm`) i oznaka „Već urađeno" (km u `PerformedAtMileageKm`).
Ako usluga na vozilu nikad nije rađena, računa se od početka: cijela kilometraža i cijela
starost vozila.

Primjeri obrazloženja (format iz mockup-a):

- *„od zadnje usluge prešli ste 23417 km (interval: 20000 km)"*
- *„vozilo ima 243100 km (interval: 20000 km)"*
- *„od zadnje usluge je prošlo 13 mjeseci (preporuka na 1 god.)"*
- *„vozilo je staro 4 godine (preporuka na 2 god.)"*

### 5.3. Motor

Usluga koja navodi samo neke tipove motora (npr. čišćenje DPF filtera samo za dizel) dobija
puni doprinos i obrazloženje *„namijenjeno vozilima sa motorom: dizel"*. Usluga za sve motore
(bez liste ili sa svim tipovima) dobija pola doprinosa i **ne navodi se kao razlog**, jer ništa
ne govori o konkretnom vozilu.

### 5.4. Ocjena

Prosjek vidljivih recenzija, centriran na 3★. Ocjena 5,0 dodaje 0,10, ocjena 1,0 oduzima 0,10.
Usluga bez recenzija ne dobija ništa.

### 5.5. Marka i model

Iz svih završenih rezervacija, svih kupaca, broji se koliko puta je svaka usluga rađena na
vozilima iste marke i istog modela. Brojevi se dijele najvećim, pa najčešća usluga ima udio 1.
Poklapanje modela vrijedi više od poklapanja marke (faktor 0,6 za marku). Obrazloženje: *„često
se radi na vozilima Audi A4"*.

Uz prosječnu ocjenu, ovo je jedini dio koji koristi podatke drugih kupaca. Oba su agregati:
nijedan pojedinačni kupac se ne vidi.

### 5.6. Kazna za nedavno urađeno

Ako je usluga obavljena, a prošlo je manje od pola intervala (omjer < 0,5), skor se umanjuje za
0,50. Lista se tako ne puni poslom koji je vozilo upravo imalo. Kazna se ne prikazuje kao razlog
preporuke.

---

## 6. Filteri

Prije bodovanja usluga se **izbacuje** ako:

1. navodi tipove motora, a motor vozila nije među njima (benzinac nikad ne dobije dizel uslugu,
   ni obrnuto);
2. već postoji otvorena rezervacija za to vozilo koja je sadrži (status nije `Completed` ni
   `Cancelled`).

Neaktivne usluge i usluge u neaktivnim kategorijama nisu ni u katalogu.

---

## 7. Rangiranje

1. Usluge se sortiraju po skoru, opadajuće. Pri jednakom skoru odlučuje ID usluge, pa je
   redoslijed uvijek isti za iste podatke.
2. Iz svake kategorije ulaze **najviše 2** usluge, da npr. pet varijanti zamjene filtera ne
   popuni cijelu listu.
3. Vraća se prvih **5** (`RECOMMENDER_TOP_N`).

**Hitnost:** preporuka je `Urgent` („Hitno") kad je omjer dospjelosti ≥ **1,0**
(`URGENCY_HITNO_THRESHOLD`), tj. interval je prošao. Inače je `Planned` („Planirano").

---

## 8. Objašnjenje

Svaka preporuka ima objašnjenje: do **3** najjača pozitivna faktora sa doprinosom ≥ **0,05**
(`EXPLANATION_MIN_CONTRIBUTION`), spojena znakom „;". Ako nijedan ne prelazi prag, uzima se
najjači faktor. Sličnost postoji uvijek, pa objašnjenje nikad nije prazno.

Pored teksta, API vraća i **sve faktore** sa doprinosima (`factors`), pa klijent može prikazati
detaljno zašto je usluga preporučena.

Primjer stvarnog odgovora za seed korisnika `nedim`, Audi A4 iz 2012., 310 500 km, dizel:

| # | Hitnost | Usluga | Objašnjenje |
|---|---|---|---|
| 1 | Planirano | Zamjena filtera goriva | već ste koristili usluge iz kategorije „Redovno održavanje"; od zadnje usluge prešli ste 35732 km (interval: 40000 km); namijenjeno vozilima sa motorom: dizel |
| 2 | Hitno | Balansiranje točkova | već ste koristili usluge iz kategorije „Gume"; od zadnje usluge prešli ste 29285 km (interval: 20000 km); često se radi na vozilima Audi A4 |
| 3 | Hitno | Pregled kočnica | već ste koristili usluge iz kategorije „Kočnice"; od zadnje usluge prešli ste 35624 km (interval: 20000 km); često se radi na vozilima Audi A4 |
| 4 | Hitno | Zamjena vodene pumpe | već ste koristili usluge iz kategorije „Motor"; vozilo ima 310500 km (interval: 100000 km); namijenjeno vozilima sa motorom: dizel |
| 5 | Hitno | Zamjena filtera zraka | već ste koristili usluge iz kategorije „Redovno održavanje"; vozilo ima 310500 km (interval: 30000 km); namijenjeno vozilima sa motorom: dizel |

Ista lista za benzinsku Toyotu Corollu iz 2023. (12 300 km, korisnik `lejla`) ne sadrži nijednu
dizel uslugu, a vodi pregled kočnica jer je od zadnjeg prošlo 15 mjeseci.

---

## 9. Kako se preporuke mijenjaju

| Događaj | Efekat |
|---|---|
| Kupac pregleda ili pretraži usluge | Blagi pomak profila prema tim uslugama (0,5 i 0,3) |
| Klik na „Rezerviši" | Signal 1,0. U logu preporuka se ta preporuka označi kao kliknuta |
| Rezervacija | Signal 2,0. Usluga nestaje sa liste dok je rezervacija otvorena |
| Završen servis | Signal 3,0. Brojač intervala kreće od tog datuma i kilometraže, a usluga pada zbog kazne za nedavno urađeno |
| „Već urađeno" | Signal 1,5. Brojač intervala kreće od tog datuma i trenutne kilometraže, a otvoreni podsjetnici za tu uslugu se zatvaraju |
| Otkazivanje / loša ocjena | Negativan signal udaljava profil od te usluge i njoj sličnih |
| Vrijeme prolazi | Stari signali slabe (poluživot 180 dana), a dospjelost raste dok usluga ne postane hitna |
| Kilometraža se ažurira | Dospjelost po km se odmah preračunava |

Preporuke se ne računaju unaprijed. Računaju se pri svakom zahtjevu, pa promjena odmah utiče na
sljedeći prikaz.

---

## 10. Endpointi

| Metoda i putanja | Opis |
|---|---|
| `GET /api/recommendations?vehicleId=` | Lista preporuka za vozilo, sa objašnjenjem i faktorima |
| `POST /api/recommendations/{serviceId}/clicked` | Body `{ "vehicleId" }`. Bilježi klik na „Rezerviši" |
| `POST /api/recommendations/{serviceId}/done` | Body `{ "vehicleId", "performedAtUtc"? }`. „Već urađeno". Datum ne smije biti u budućnosti, a bez datuma se uzima današnji |

Sva tri endpointa traže ulogu Customer. Vozilo mora pripadati prijavljenom korisniku.

---

## 11. Bilježenje i revizija

Svaki prikazani skup upisuje se u `RecommendationLogs`: korisnik, vozilo, usluga, skor, rang,
hitnost, tekst objašnjenja, svi faktori kao JSON, vrijeme i verzija algoritma. Klik postavlja
`WasClicked`. Iz loga se može naknadno provjeriti zašto je neka usluga bila preporučena i
izračunati koliko se preporuka pretvara u rezervacije.

---

## 12. Performanse

Jedan zahtjev izvršava fiksan broj upita, bez upita u petlji:

1. vozilo;
2. katalog (iz keša, inače jedan upit sa oznakama, motorima i ocjenama);
3. signali kupca;
4. zadnja obavljanja (završene stavke i oznake „Već urađeno");
5. statistika marke i modela (jedan `GROUP BY`);
6. otvorene rezervacije vozila;
7. upis loga.

Sam algoritam je čista funkcija nad podacima u memoriji, linearna u broju usluga i signala.

---

## 13. Testovi

**Unit testovi** (`SmartGarage.UnitTests/Recommender/RecommenderEngineTests.cs`) provjeravaju
algoritam na malom ručno sastavljenom katalogu:

- tip motora je tvrdi filter u oba smjera;
- novi kupac dobija punu listu, svaka stavka sa razlogom;
- isti ulaz uvijek daje isti rezultat;
- usluga daleko preko intervala je hitna, a obrazloženje je u formatu iz mockup-a (km i godine);
- nedavno urađena usluga pada, nije hitna i kazna se ne prikazuje kao razlog;
- nova historija mijenja rang i obrazloženje;
- negativni signali spuštaju uslugu koju bi pozitivni podigli;
- stari signali vrijede manje od novih;
- statistika marke i modela podiže uslugu i navodi se kao razlog;
- samo usluga specifična za motor navodi motor kao razlog;
- već rezervisane usluge se ne nude, a nijedna kategorija nema više od dva mjesta.

**Integracioni testovi** (`SmartGarage.IntegrationTests/Recommendations/RecommendationTests.cs`)
provjeravaju preko HTTP-a i stvarne baze:

- preporuke su samo za vlastito vozilo (tuđe daje `400`, a zaposlenik dobija `403`);
- svaka preporuka ima objašnjenje, a prikaz se upisuje u log;
- pregled i pretraga kataloga upisuju signale, ali samo za kupca;
- klik upisuje signal i označava log;
- „Već urađeno" upisuje kilometražu, odbija datum u budućnosti i spušta uslugu na listi.

---

## 14. Konfiguracija (`.env`)

| Ključ | Vrijednost | Značenje |
|---|---|---|
| `RECOMMENDER_TOP_N` | 5 | Broj preporuka |
| `RECOMMENDER_ALGORITHM_VERSION` | cbf-1.0 | Upisuje se u log |
| `SIGNAL_HALF_LIFE_DAYS` | 180 | Poluživot signala |
| `SIGNAL_SATURATION_COUNT` | 8 | Količina historije pri kojoj ona dobija punu težinu |
| `HISTORY_BLEND_BETA` | 0,7 | Udio historije u profilu kad je historije dovoljno |
| `HIGH_MILEAGE_THRESHOLD_KM` | 150000 | Prag za oznaku „velika kilometraža" |
| `OLD_VEHICLE_AGE_YEARS` | 10 | Prag za oznaku „starije vozilo" |
| `W_ENGINE` | 0,20 | Težina poklapanja motora |
| `W_MILEAGE` | 0,30 | Težina dospjelosti (km i vrijeme) |
| `W_AGE` | 0,10 | Težina raspona starosti |
| `W_RATING` | 0,10 | Težina ocjene |
| `W_BRAND_MODEL` | 0,15 | Težina statistike marke i modela |
| `W_RECENT` | 0,50 | Kazna za nedavno urađeno |
| `EXPLANATION_MIN_CONTRIBUTION` | 0,05 | Najmanji doprinos da faktor bude razlog |
| `URGENCY_HITNO_THRESHOLD` | 1,0 | Omjer dospjelosti od kojeg je preporuka hitna |
