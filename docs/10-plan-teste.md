# 10. Plan de teste

Cum se verifică robotul `PixelDataProgramari`, de la logica C# până la programarea creată în PixelData. Nimic de aici nu se rulează pe date reale de pacienți.

Surse: `docs/sources/decizii-design-v1.md` (contract, coduri de eroare, API `robot/Logic`) și `docs/sources/investigatie-2026-09-10.md` (scenarii de eșec pe laptop).

## Niveluri

| Nr. | Nivel | Ce prinde | Unde rulează | Comandă (din rădăcina repo-ului) |
|---|---|---|---|---|
| 1 | Teste unitare C# | reguli de date: parsare item, CNP, nume, mapări, validare, output | laptopul Windows (.NET 8 SDK) | `dotnet test tests/Logic.Tests` |
| 2 | Teste Go | clientul de coadă `tools/queue-client` | orice mașină cu Go | `go test ./...` (în `tools/queue-client`) |
| 3 | Exemple de contract | schema JSON a itemului și exemplele | orice mașină cu Python 3 + `jsonschema` | `python3 contracts/validate_examples.py` |
| 4 | Workflow-uri în Studio | fiecare pas UI în PixelData, izolat | laptopul, UiPath Studio | din Studio (vezi §4) |
| 5 | End-to-end | coadă → Orchestrator → robot → PixelData | laptopul + Orchestrator | vezi §5 |
| 6 | Matrice de eșecuri | comportamentul la căderi și date greșite | laptopul + Orchestrator | vezi §6 |

Ordinea contează: un nivel se rulează doar după ce nivelurile anterioare trec.

## 1. Teste unitare C# (`tests/Logic.Tests`)

Ce sunt:
- Proiect xUnit separat, țintă `net8.0`, C# 10. Stă ÎN AFARA proiectului UiPath, fiindcă Studio compilează toate fișierele `.cs` din folderul proiectului.
- Compilează direct fișierele din `robot/Logic` (`<Compile Include="../../robot/Logic/**/*.cs" />`), deci testează exact codul copiat în Studio.
- Copiază în folderul de output exemplele din `contracts/examples/` și `robot/Data/pixeldata-mappings.json`.
- Nu citesc ceasul: fiecare test primește un `now` fix.

Pregătire pe laptop (o singură dată):
1. Instalează .NET 8 SDK (conține și runtime-ul .NET 8 cerut de `net8.0`). Un SDK mai nou fără runtime-ul 8 nu rulează testele (de verificat pe laptop).
2. Verifică: `dotnet --list-sdks` afișează o versiune `8.x`.

Rulare:
```
dotnet test tests/Logic.Tests
```
Doar o clasă de teste:
```
dotnet test tests/Logic.Tests --filter "FullyQualifiedName~CnpValidatorTests"
```

Clase de teste:

| Fișier | Acoperă |
|---|---|
| `QueueItemParserJsonTests.cs` | `QueueItemParser.FromJson`: exemplele valide, chei lipsă, formate greșite, versiune de schemă, operație, chei necunoscute |
| `QueueItemParserSpecificContentTests.cs` | `QueueItemParser.FromSpecificContent`: tipurile de valori primite din Studio (string, bool, numere, `DateTime`, `DateTimeOffset`) |
| `CnpValidatorTests.cs` | `CnpValidator`: cifra de control, lungime, caractere, lună/zi, secol, data nașterii |
| `NameSplitterTests.cs` | `NameSplitter`: Nume / Prenume |
| `PixelDataMappingsTests.cs` | `PixelDataMappings`: resursă, sursa pacientului, proceduri, trimitere |
| `PixelDataMappingsFileTests.cs` | fișierul real `robot/Data/pixeldata-mappings.json` (valorile `TODO` dau `RESOURCE_MAPPING_MISSING`) |
| `AppointmentValidatorTests.cs` | `AppointmentValidator`: fiecare regulă, ordinea regulilor, opțiunile |
| `ValidExamplesTests.cs` | fiecare fișier din `contracts/examples/valid/` trece și regulile robotului (cu `RequireCnp` = false) |
| `BusinessInvalidExamplesTests.cs` | fiecare fișier din `contracts/examples/invalid/business/` e respins cu codul așteptat |
| `BusinessRuleViolationTests.cs` | mesajul `<code>: <message>` |
| `ResultObjectsTests.cs` | `ValidationIssue`, `CnpCheckResult`, `PersonName` |
| `ErrorCodesTests.cs` | `ErrorCodes.IsBusiness` pentru fiecare cod |
| `TransactionOutputTests.cs` | cheile și tipurile din `TransactionOutput.Success` |

Testele au fost scrise pe Linux, fără compilator. La prima rulare pe laptop pot apărea erori de compilare; se corectează testul sau codul, după cum spune `docs/sources/decizii-design-v1.md` §„API C# `robot/Logic`”.

## 2. Teste Go (`tools/queue-client`)

```
cd tools/queue-client
go test ./...
```
Ce face clientul și cum se configurează: [tools/queue-client/README.md](../tools/queue-client/README.md).

## 3. Exemple de contract

```
python3 contracts/validate_examples.py
```

| Folder | Rezultat așteptat |
|---|---|
| `contracts/examples/valid/` | trece schema |
| `contracts/examples/invalid/schema/` | NU trece schema, fiecare fișier dintr-un singur motiv |
| `contracts/examples/invalid/business/` | trece schema, dar robotul îl respinge cu un cod de eroare business (verificat în nivelul 1) |

Fiecare fișier conține DOAR obiectul `SpecificContent` (fără învelișul `{"itemData": ...}`). Motivul fiecărui fișier invalid: [contracts/examples/invalid/README.md](../contracts/examples/invalid/README.md).

Schema verifică formatele (UUID, dată, oră, offset, CNP, telefon) cu `pattern`, nu cu `format`: `jsonschema` verifică `"format": "date-time"` doar dacă e instalat pachetul `rfc3339-validator`, deci rezultatul nu depinde de pachetele instalate. Validitatea calendaristică (de exemplu `2030-02-30`) și cifra de control a CNP-ului le verifică robotul, nu schema.

## 4. Workflow-uri în Studio (pe laptop)

Scop: fiecare workflow XAML merge singur, înainte de a fi legat în REFramework. Numele și argumentele workflow-urilor sunt descrise în documentația robotului (docs/08, de verificat numele exacte).

Reguli:
- Doar în PixelData cu pacientul de test și resursa de test (vezi §8).
- Rezoluția și scalarea ecranului identice cu cele de pe robot (ideal 100%).
- Nu se depanează în Studio cât rulează un job unattended pe același laptop.

Pentru fiecare workflow:
1. Deschide workflow-ul, completează valorile implicite ale argumentelor de intrare cu date de test.
2. Rulează cu Debug File; urmărește fiecare pas.
3. Notează ce s-a întâmplat în tabelul de mai jos.

| Pas | Ce se verifică |
|---|---|
| Pornire + login | PixelData pornește; login cu asset-ul `PixelData_RobotLogin`; o instanță veche din sesiunea robotului e închisă, nu cele din alte sesiuni |
| Calendar + resursă | se alege data corectă din mini-calendar și resursa din mapări |
| Căutare pacient | după CNP: găsit / negăsit / mai mulți pacienți (`PATIENT_AMBIGUOUS`) |
| Creare pacient („Date demografice”) | Nume, Prenume, CNP completate; CNP greșit ⇒ popup „Eroare” ⇒ `PIXELDATA_CNP_REJECTED` |
| Deschidere slot | dublu-click pe ora cerută; slot ocupat ⇒ `SLOT_OCCUPIED` |
| „Pacient si documente” | `Trimitere`, `Nr./Serie bilet`, `Data bilet`, `Sursa pacient:`, `Medic trimitator:`, `Stare:` |
| Proceduri | procedura din mapări sau `ProductName`, cantitate 1 |
| Salvare | nu apare popup „Atenție” („Nu ați selectat starea pacientului!”); slotul apare ocupat în grilă |
| Verificare idempotentă | programarea există deja (CNP + dată + oră + resursă) ⇒ nu se creează a doua |

## 5. End-to-end (pe laptop)

Precondiții:
- Orchestrator: folderul `PixelData` cu coada `PixelData_Programari`, procesul `PixelDataProgramari`, triggerul `PixelData_Programari_OnNewItem`, robotul conectat (docs/06-setup-orchestrator.md, docs/07-setup-laptop.md).
- PixelData: pacient de test și resursă de test disponibile (de verificat cu administratorul PixelData).
- `robot/Data/pixeldata-mappings.json`: resursa de test mapată (fără `TODO` pe rândul folosit).
- Omul s-a delogat din Windows (nu Win+L).

Pași:
1. Pregătește un item: copiază un fișier din `contracts/examples/valid/` și schimbă `AppointmentId` (UUID nou), datele pacientului de test, `ScheduledAt` / `ScheduledLocalDate` / `ScheduledLocalTime` pe un slot liber din viitor, `BranchName` pe sucursala mapată la resursa de test.
2. Trimite itemul în coadă cu `tools/queue-client`. Comanda exactă: [tools/queue-client/README.md](../tools/queue-client/README.md).
3. În Orchestrator: itemul apare `New`, triggerul pornește un job, itemul trece `InProgress` → `Successful`.
4. În PixelData: slotul e ocupat și afișează pacientul, telefonul și serviciul; câmpurile din „Pacient si documente” corespund itemului.
5. În Orchestrator, pe item, verifică Output:

   | Cheie | Valoare așteptată |
   |---|---|
   | `OutputSchemaVersion` | `"1"` |
   | `Outcome` | `"created"` |
   | `PatientCreated` | `true` dacă pacientul de test nu exista, altfel `false` |
   | `PixelDataResource` | resursa de test |
   | `ProcessedAt` | data și ora procesării, RFC3339 |

6. Idempotență la coadă: trimite din nou același item (același `AppointmentId`, deci același Reference `create-<AppointmentId>`). Orchestrator îl respinge („Duplicate Reference”; codul HTTP exact de verificat).
7. Idempotență în robot: creează manual în PixelData o programare pentru pacientul de test pe un alt slot liber, apoi trimite un item nou (alt `AppointmentId`) cu același CNP, dată, oră și resursă. Așteptat: `Successful`, `Outcome` = `"already_existed"`, nicio programare dublă în PixelData.

Curățenie după test:
- Șterge din PixelData programările de test (procedura exactă de verificat în PixelData).
- Pacientul de test rămâne rezervat testelor; nu se refolosește pentru pacienți reali.
- Șterge din `Exceptions_Screenshots` și din logurile robotului fișierele create în timpul testului.

## 6. Matrice de eșecuri

Legendă status item (Orchestrator):
- Excepție business: `Failed`, fără retry.
- Excepție system (application): prima încercare devine `Retried` și Orchestrator creează un item nou cu același Reference; a doua încercare ajunge `Successful` sau `Failed` (coada are Auto retry, maximum 1).
- `ProcessingException` începe cu codul, de exemplu `CNP_INVALID: <mesaj>`.

| Nr. | Scenariu | Cum se provoacă | Status item așteptat | Cod / observații |
|---|---|---|---|---|
| E1 | Laptop oprit | oprește laptopul, trimite un item | `New` până pornește robotul; jobul stă `Pending`; triggerul reverifică la 30 min | după pornire: `Successful`; dacă ora programării a trecut între timp: `Failed`, `APPOINTMENT_IN_PAST` |
| E2 | Laptop fără rețea | deconectează rețeaua, trimite un item | robot deconectat după 2 min fără heartbeat; item `New` | la reconectare se procesează normal |
| E3 | Sleep în mijlocul jobului | pune laptopul în sleep după deschiderea fișei, înainte de salvare | item `InProgress`; după 24h `Abandoned`; retry ⇒ item nou | la retry, verificarea idempotentă: `Successful` cu `created` sau `already_existed`; exact O programare în PixelData |
| E4 | Popup „Eroare” (CNP respins de PixelData) | pacient nou cu un CNP pe care PixelData îl refuză (de verificat dacă există un CNP valid local dar refuzat de PixelData) | `Failed` | `PIXELDATA_CNP_REJECTED` |
| E5 | Popup „Atenție” („Nu ați selectat starea pacientului!”) | `initialStatus` greșit în mapări, pe o copie de test | `Retried` → `Failed` | cod de eroare system conform docs/08 (de verificat; candidat `PIXELDATA_SAVE_UNCONFIRMED`); popup-ul închis cu „Închide” |
| E6 | Popup „Atenționare” (eliminarea fișei) / „Atenție” (golirea câmpurilor) | anulare în timpul completării, doar în Studio | nu se pierd date; răspunsul robotului conform docs/08 (de verificat) | apariție neașteptată în job ⇒ excepție system, `PIXELDATA_UI_TIMEOUT` (de verificat) |
| E7 | CNP cu cifra de control greșită | item din `contracts/examples/invalid/business/` | `Failed` | `CNP_INVALID`; PixelData nu e atins |
| E8 | CNP gol | item cu `PatientCnp` = `""` | `Failed` | `CNP_REQUIRED` (cât timp `RequireCnp` = true) |
| E9 | Slot ocupat | ocupă manual slotul în PixelData cu alt pacient de test, apoi trimite itemul | `Failed` | `SLOT_OCCUPIED` |
| E10 | Pacient ambiguu | doi pacienți de test care se potrivesc la căutare | `Failed` | `PATIENT_AMBIGUOUS` |
| E11 | Resursă nemapată | sucursală cu `pixelDataResource` = `"TODO"` | `Failed` | `RESOURCE_MAPPING_MISSING` |
| E12 | Sursa pacientului nemapată | `patientSource.fallback` = `"TODO"` și clinică negăsită | `Failed` | `PATIENT_SOURCE_MAPPING_MISSING` |
| E13 | Login PixelData eșuat | parolă greșită în asset-ul `PixelData_RobotLogin` | `Retried` → `Failed` | `PIXELDATA_LOGIN_FAILED`; atenție la blocarea contului după încercări repetate (de verificat) |
| E14 | PixelData nu pornește | cale greșită spre executabil, pe o copie de test | `Retried` → `Failed` | `PIXELDATA_UNAVAILABLE` |
| E15 | Rezoluție schimbată | conectează un monitor extern sau schimbă scalarea | `Retried` → `Failed` dacă țintirea nu mai găsește elementele | `PIXELDATA_UI_TIMEOUT` (de verificat); se revine la rezoluția de dev |
| E16 | Sesiune Windows blocată | Win+L în loc de delogare | `Retried` → `Failed` | eroarea „Cannot bring the target application in foreground because the Windows session is locked”; `PIXELDATA_UI_TIMEOUT` (de verificat) |
| E17 | Salvare neconfirmată | slotul nu apare ocupat după salvare | `Retried`; la retry verificarea idempotentă evită dublura | `PIXELDATA_SAVE_UNCONFIRMED` |
| E18 | PixelData deschis de un om în altă sesiune | un utilizator lasă PixelData deschis în sesiunea lui | `Successful` | sesiunea omului rămâne neatinsă (fără Kill Process după nume) |
| E19 | Contract greșit | `SchemaVersion` = `"2"` / `Operation` = `"cancel"` / cheie lipsă | `Failed` | `UNSUPPORTED_SCHEMA_VERSION` / `UNSUPPORTED_OPERATION` / `MISSING_FIELD` |
| E20 | Programare în trecut | `ScheduledAt` în trecut | `Failed` | `APPOINTMENT_IN_PAST` |
| E21 | Item dublu | același `AppointmentId` trimis de două ori | al doilea nu intră în coadă | „Duplicate Reference” la `AddQueueItem` |
| E22 | Procedură inexistentă în PixelData | item cu `ProductName` (sau `procedures` din mapări) care nu e în lista de proceduri din PixelData | `Failed`, excepție business, fără retry | `PROCEDURE_NOT_FOUND`; operatorul adaugă procedura în PixelData sau corectează maparea, apoi retrimite (U7, S8) |
| E23 | Medic trimițător inexistent în PixelData | item cu `ReferringDoctorName` ne-gol care nu e în lista „Medic trimitator” | `Failed`, excepție business, fără retry | `REFERRING_DOCTOR_NOT_FOUND`; `ReferringDoctorName` gol nu e eroare; operatorul adaugă medicul în PixelData sau corectează numele în recepție, apoi retrimite (U7, S8) |

## 7. Checklist de acceptanță

- [ ] Nivel 1: `dotnet test tests/Logic.Tests` trece integral pe laptop.
- [ ] Nivel 2: `go test ./...` trece în `tools/queue-client`.
- [ ] Nivel 3: `python3 contracts/validate_examples.py` raportează fiecare fișier cu rezultatul așteptat.
- [ ] Nivel 4: fiecare pas din tabelul §4 a mers în Studio.
- [ ] Nivel 5: programarea de test creată corect; Output corect; „Duplicate Reference” la retrimitere; `already_existed` fără dublură.
- [ ] Nivel 6: fiecare rând E1–E23 rulat o dată, cu statusul și codul notate; un rând nerulat are motivul scris.
- [ ] Nicio programare dublă în PixelData după testele E3 și E17.
- [ ] Curățenia din §5 făcută.

## 8. Igiena datelor de test

- Doar pacienți de test: nume fictive cu prefix („TEST POPESCU ION”), CNP fictiv cu cifra de control validă, telefon `+4070000000x`.
- Niciodată date reale de pacienți în `contracts/examples/`, în teste, în tichete sau în capturi atașate.
- `Exceptions_Screenshots` și logurile robotului conțin date de pacient (ecranul PixelData, valorile din item). Nu se trimit mai departe, nu se pun în git (`.gitignore` exclude `Exceptions_Screenshots/` și `*.log`) și se șterg după test.
- Itemii din coadă conțin datele pacientului (decizia din `docs/sources/decizii-design-v1.md`); riscul GDPR e descris în [docs/09-licente-gdpr-riscuri.md](09-licente-gdpr-riscuri.md).
