# 03 — Contractul cozii (v1)

Descrierea pentru oameni a itemului din coada `PixelData_Programari`. Adevărul pentru mașini sunt schemele; la orice diferență, schema are dreptate și acest document se corectează.

| Fișier | Ce descrie |
|---|---|
| [`contracts/appointment-queue-item.v1.schema.json`](../contracts/appointment-queue-item.v1.schema.json) | `SpecificContent` trimis în coadă (JSON Schema 2020-12) |
| [`contracts/queue-item-output.v1.schema.json`](../contracts/queue-item-output.v1.schema.json) | `Output` setat de robot la succes |

De unde vine fiecare valoare în recepție: [04](04-mapare-campuri.md).

## 1. Plicul AddQueueItem

```json
{
  "itemData": {
    "Name": "PixelData_Programari",
    "Priority": "Normal",
    "Reference": "create-<AppointmentId>",
    "SpecificContent": { "SchemaVersion": "1", "Operation": "create" }
  }
}
```

| Câmp | Valoare v1 |
|---|---|
| `Name` | `PixelData_Programari` |
| `Priority` | `Normal` |
| `Reference` | `create-<AppointmentId>` (§4) |
| `SpecificContent` | toate cheile din §3 |
| `DeferDate`, `DueDate` | nefolosite |

Apelul complet (token, headere, folder): investigatie §4 „Declanșare din backend”; implementarea de test: [`tools/queue-client/`](../tools/queue-client/).

## 2. Reguli

| Regulă | Detaliu |
|---|---|
| Chei plate | niciun obiect sau listă imbricată |
| Mereu prezente | toate cele 33 de chei din §3 sunt `required`; o cheie lipsă pică schema, iar robotul dă `MISSING_FIELD` |
| `""` = necunoscut | la texte `""`, niciodată `null`; `DurationMinutes` = `0` când e necunoscut; `ReferralPending` are mereu valoare booleană |
| Ne-gol | cel puțin un caracter care nu e spațiu |
| Chei necunoscute | permise de schemă (`additionalProperties: true`) și ignorate de robot |
| UUID | litere mici, forma 8-4-4-4-12 |
| Momente | `ScheduledAt`, `CreatedAt`: RFC3339 cu offset obligatoriu, `YYYY-MM-DDTHH:MM:SS`, fracțiune de secundă opțională (1–9 cifre), apoi `Z` sau `+HH:MM` / `-HH:MM`; `T` și `Z` cu majuscule; ex. `2026-09-15T09:30:00+03:00` |
| Dată și oră locală | `ScheduledLocalDate` `YYYY-MM-DD`, `ScheduledLocalTime` `HH:MM` (00:00–23:59), în Europe/Bucharest, gata de tastat; schema nu verifică potrivirea cu `ScheduledAt` |
| Date calendaristice | `ReferralDate`, `PatientBirthDate`: `YYYY-MM-DD` sau `""`; schema verifică doar intervalul lunii și al zilei, data reală o verifică robotul |
| Dimensiune | `SpecificContent` ≤ „256,000 characters or 512,000 bytes” ([about-queues-and-transactions](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-queues-and-transactions#schema-definitions)) |
| Date de test | doar fictive, inclusiv CNP-urile |

## 3. Cheile `SpecificContent`

„Ne-gol” = schema cere o valoare ne-goală. „Robotul cere” = schema acceptă `""`, dar validarea robotului refuză itemul.

| Cheie | Tip / format | Ne-gol | Valori / observații |
|---|---|---|---|
| `SchemaVersion` | string | da | `"1"` |
| `Operation` | string | da | `"create"`; v1 nu are altă operație |
| `AppointmentId` | string UUID | da | |
| `CreatedAt` | string RFC3339 cu offset | da | |
| `Source` | string | nu | `""`, `"Centrala"`, `"Aplicație"`, `"La sediu"`; canalul programării, NU „Sursa pacient” din PixelData |
| `ScheduledAt` | string RFC3339 cu offset | da | |
| `ScheduledLocalDate` | string `YYYY-MM-DD` | da | Europe/Bucharest |
| `ScheduledLocalTime` | string `HH:MM` | da | Europe/Bucharest |
| `DurationMinutes` | integer ≥ 0 | nu | `0` = necunoscut |
| `BranchId` | string UUID sau `""` | nu | |
| `BranchName` | string | nu | robotul cere: gol ⇒ `MISSING_FIELD`; ex. „ScanExpert Galați” |
| `Modality` | string | nu | ex. RMN, CT, Ecografie |
| `ProductId` | string UUID sau `""` | nu | |
| `ProductCode` | string | nu | codul de catalog; de confirmat că e „Cod” din PixelData |
| `ProductName` | string | da | |
| `Laterality` | string | nu | `""`, `"stanga"`, `"dreapta"`, `"bilateral"` |
| `Payer` | string | nu | `""`, `"CAS"`, `"Monitor"`, `"Contra cost"`, `"Asigurator privat"`; `CAS`/`Monitor` ⇒ „Trimitere” |
| `Insurer` | string | doar la `Payer` = `"Asigurator privat"` | impus de schemă (`if`/`then`) și de validatorul robotului (`MISSING_FIELD`) |
| `ReferralDate` | string `YYYY-MM-DD` sau `""` | nu | „Data bilet” |
| `ReferralPending` | boolean | — | `true` = biletul nu a sosit încă |
| `ReferralNumber` | string | nu | „Nr./Serie bilet”; `""` în v1 (recepția nu îl are) |
| `ReferringDoctorName` | string | nu | „Medic trimițător” |
| `ReferringDoctorClinic` | string | nu | intrare pentru „Sursa pacient” |
| `ReferringDoctorParafa` | string | nu | |
| `PatientId` | string UUID | da | |
| `PatientFullName` | string | da | |
| `PatientLastName` | string | nu | `""` ⇒ robotul împarte `PatientFullName` |
| `PatientFirstName` | string | nu | |
| `PatientCnp` | string: 13 cifre sau `""` | nu | schema verifică doar cele 13 cifre; cifra de control și data le verifică robotul; gol ⇒ `CNP_REQUIRED` (implicit) |
| `PatientPhone` | string E.164 (`+`, prima cifră 1–9, 8–15 cifre) sau `""` | nu | ex. `+40...` |
| `PatientBirthDate` | string `YYYY-MM-DD` sau `""` | nu | |
| `PatientSex` | string | nu | valori de verificat (recepția: `''`, `M`, `F`) |
| `Notes` | string ≤ 2000 caractere | nu | „Observații” |

## 4. Reference

| Regulă | Detaliu |
|---|---|
| Formă | `create-<AppointmentId>`, ex. `create-00000000-0000-4000-8000-000000000001` |
| Limite | al nostru are 43 de caractere. Limita de 128 și interdicția apostrofului sunt documentate pentru **expresia de filtrare**, nu pentru câmpul stocat ([get-queue-items](https://docs.uipath.com/activities/other/latest/workflow/get-queue-items)); o sursă de forum spune că și câmpul refuză peste 128 (de verificat) |
| Unicitate | coada are „Enforce unique references” = ON; al doilea item cu același `Reference` e respins cu `errorCode` `1016 DuplicateReference` (cod HTTP nedocumentat oficial, de verificat). Verificarea „applies to all transactions except deleted or retried ones” ([about-queues-and-transactions](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-queues-and-transactions)), deci un item `Failed` ține `Reference`-ul ocupat |
| Emitentul | tratează duplicatul ca „deja în coadă”, nu ca eroare |
| Retry | clonele create de Auto retry păstrează `Reference`-ul; itemul actual e cel cu `Id` maxim |
| Retrimitere după un eșec Business corectat | Edit pe Specific Data + marcare `Retried`, sau ștergerea itemului `Failed` și retrimiterea sub același `Reference`. `Reference` NU capătă niciodată sufix: e cheia de idempotență. Procedura: [05](05-integrare-receptie.md) §6, [06](06-setup-orchestrator.md) §10 |

## 5. Output la succes

Chei necunoscute permise.

| Cheie | Tip | Observații |
|---|---|---|
| `OutputSchemaVersion` | string `"1"` | |
| `Outcome` | `"created"` sau `"already_existed"` | `already_existed` = verificarea idempotentă a găsit programarea deja în PixelData |
| `PatientCreated` | boolean | robotul a creat un pacient nou în PixelData („Date demografice”) |
| `PixelDataResource` | string ne-gol | resursa folosită, din `robot/Data/pixeldata-mappings.json` |
| `ProcessedAt` | string RFC3339 cu offset obligatoriu | același format ca `ScheduledAt` |

## 6. Eșec și coduri de eroare

| Regulă | Detaliu |
|---|---|
| Unde | Orchestrator păstrează excepția în `ProcessingException` (subcâmpul exact, de verificat) |
| Formă | mesajul începe cu codul: `CNP_INVALID: <mesaj>` |
| Business | în XAML: `catch BusinessRuleViolation ex` → `throw new BusinessRuleException(ex.Message)`; fără retry |
| System | excepție de aplicație; Auto retry (maximum 1) |
| Conținut | mesajul nu conține date de pacient ([09](09-licente-gdpr-riscuri.md)) |

**Business** — problema e în date.

| Cod | Când |
|---|---|
| `MISSING_FIELD` | cheie lipsă din item, sau câmp cerut gol: `ProductName`, `PatientFullName`, `BranchName`, `Insurer` la `Asigurator privat` |
| `INVALID_FIELD` | valoare în format sau tip greșit (dată, oră, UUID, boolean) |
| `UNSUPPORTED_SCHEMA_VERSION` | `SchemaVersion` ≠ `"1"` |
| `UNSUPPORTED_OPERATION` | `Operation` ≠ `"create"` |
| `CNP_REQUIRED` | `PatientCnp` gol și robotul cere CNP (implicit) |
| `CNP_INVALID` | `PatientCnp` prezent, dar cifra de control sau data sunt greșite |
| `APPOINTMENT_IN_PAST` | `ScheduledAt` e în trecut la procesare |
| `RESOURCE_MAPPING_MISSING` | nicio resursă PixelData pentru `BranchName` + `Modality`, sau valoarea e `"TODO"` |
| `PATIENT_SOURCE_MAPPING_MISSING` | „Sursa pacient” nu se poate determina din mapări |
| `PATIENT_AMBIGUOUS` | căutarea pacientului în PixelData nu dă un rezultat unic |
| `SLOT_OCCUPIED` | slotul orar e ocupat în PixelData |
| `PIXELDATA_CNP_REJECTED` | PixelData refuză CNP-ul („Eroare CNP pacient!”) |
| `PROCEDURE_NOT_FOUND` | procedura rezolvată (din `procedures` în mapări, altfel `ProductName`) nu e în lista de proceduri din PixelData |
| `REFERRING_DOCTOR_NOT_FOUND` | `ReferringDoctorName` e ne-gol și nu e în lista „Medic trimițător” din PixelData; un nume gol nu e eroare |

Ultimele două coduri sunt adăugiri față de lista din [`docs/sources/decizii-design-v1.md`](sources/decizii-design-v1.md), decise pe 2026-09-11. Amândouă apar în pașii UI din Studio, nu în parserul sau validatorul din `robot/Logic`. Motivul: o valoare care lipsește dintr-o listă PixelData e o problemă de date, deci itemul nu se reîncearcă. Respinse: un cod System (ar reîncerca degeaba, cu un mesaj înșelător) și completarea cu medic gol (pierdere tăcută de date pe o trimitere CAS).

**System** — problema e în aplicație sau în mediu.

| Cod | Când |
|---|---|
| `PIXELDATA_UNAVAILABLE` | PixelData nu pornește sau nu răspunde |
| `PIXELDATA_LOGIN_FAILED` | login-ul în PixelData eșuează |
| `PIXELDATA_UI_TIMEOUT` | un ecran sau element nu apare la timp |
| `PIXELDATA_SAVE_UNCONFIRMED` | după salvare, confirmarea nu apare; retry-ul trece întâi prin verificarea de existență |

Validarea are două niveluri: structura (schema și parserul robotului) și regulile (validatorul robotului și pașii din PixelData). Exemplele sunt împărțite la fel (§8).

## 7. Versionare

| Schimbare | Rămâne v1? | De ce |
|---|---|---|
| Cheie nouă, opțională | da | schema permite chei necunoscute, robotul le ignoră |
| Text, exemple, clarificări | da | nimic nu se schimbă pe fir |
| Valoare nouă într-o listă închisă (`Payer`, `Laterality`, `Source`) | nu | schema v1 și robotul v1 refuză itemul |
| Cheie scoasă sau redenumită | nu | toate cheile sunt `required` |
| Tip sau format schimbat | nu | pică schema; parserul dă `INVALID_FIELD` |
| Cheie opțională devine ne-goală, sau invers | nu | itemele vechi eșuează, sau robotul pierde o garanție |
| `Operation` nou (`cancel`, `update`) | nu | v1 acceptă doar `"create"` |

Trecerea la v2:

| Pas | Detaliu |
|---|---|
| 1 | fișier nou `contracts/appointment-queue-item.v2.schema.json` + exemple; fișierul v1 nu se schimbă incompatibil |
| 2 | robotul acceptă `"1"` și `"2"`, publicat primul |
| 3 | emitentul trimite `SchemaVersion` `"2"` |
| 4 | suportul v1 se scoate când coada nu mai are itemi v1 |

`Output` are propria versiune (`OutputSchemaVersion`) și propria schemă, cu aceleași reguli.

## 8. Exemple și validator

Fiecare exemplu conține doar obiectul `SpecificContent`, fără învelișul `itemData`.

| Loc | Conține |
|---|---|
| `contracts/examples/valid/` | itemi corecți |
| `contracts/examples/invalid/schema/` | itemi care pică schema |
| `contracts/examples/invalid/business/` | itemi care trec schema, dar robotul îi refuză cu un cod Business |
| `contracts/validate_examples.py` | verifică exemplele față de schemă |

Ce schimbă fiecare exemplu invalid și codul așteptat: [`contracts/examples/invalid/README.md`](../contracts/examples/invalid/README.md). Rulare: [`contracts/README.md`](../contracts/README.md).
