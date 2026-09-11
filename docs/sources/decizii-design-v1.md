# Decizii de design v1 (document de lucru)

Sursă comună pentru echipă.
- Proprietari: backend-engineer pentru contractul cozii; rpa-engineer pentru `robot/Logic` și fișierul de mapări.
- Orice schimbare se anunță colegilor afectați și se actualizează aici.

## Nume convenite
| Element | Nume |
|---|---|
| Folder Orchestrator | `PixelData` |
| Coadă | `PixelData_Programari` (Enforce unique references = ON, Auto retry = ON, max 1) |
| Proiect Studio / proces | `PixelDataProgramari` (REFramework, compatibilitate Windows, expresii C#) |
| Queue trigger | `PixelData_Programari_OnNewItem` (Maximum pending and running jobs = 1) |
| Credential asset login PixelData | `PixelData_RobotLogin` |
| External Application (teste acum, recepția în faza 2) | `ScanExpert-Receptie-Dispatcher` (scope `OR.Queues`) |
| Machine template | `PixelData-Laptop` |
| Robot account / cont Windows local | `robot-pixeldata` |
| Reference item coadă | `create-<AppointmentId>` |
| Namespace C# | `PixelDataProgramari.Logic` |
| Fișier mapări robot | `robot/Data/pixeldata-mappings.json` |

## Contract item coadă v1 (SpecificContent)
Reguli:
- Chei plate, fără obiecte imbricate.
- Toate cheile din tabel sunt MEREU prezente. String gol `""` = necunoscut. Cheile necunoscute se ignoră (compatibilitate cu versiuni viitoare).
- Date și ore:
  - `ScheduledAt` și `CreatedAt`: RFC3339 cu offset
  - `ScheduledLocalDate` și `ScheduledLocalTime`: ora locală Europe/Bucharest, gata de tastat
- Datele pacientului sunt în item (decizia utilizatorului), cu risc GDPR (vezi docs/09).
- Schema: `contracts/appointment-queue-item.v1.schema.json` (JSON Schema 2020-12). Exemple: `contracts/examples/valid/` și `contracts/examples/invalid/`.

| Cheie | Tip / format | Obligatoriu ne-gol | Sursă în recepție | Observații |
|---|---|---|---|---|
| SchemaVersion | string `"1"` | da | — | |
| Operation | string `"create"` | da | — | v1 doar create |
| AppointmentId | string UUID | da | appointments.id | |
| CreatedAt | string RFC3339 | da | appointments.created_at | |
| Source | `""`, `"Centrala"`, `"Aplicație"`, `"La sediu"` | nu | appointments.source | canalul programării, NU „Sursa pacient” din PixelData |
| ScheduledAt | string RFC3339 cu offset | da | appointments.scheduled_at | |
| ScheduledLocalDate | string `YYYY-MM-DD` | da | derivat (Europe/Bucharest) | |
| ScheduledLocalTime | string `HH:MM` | da | derivat (Europe/Bucharest) | |
| DurationMinutes | integer ≥ 0 | nu (0 = necunoscut) | products.duration_minutes | |
| BranchId | string UUID sau `""` | nu | appointments.branch_id | |
| BranchName | string | nu (robotul îl cere pentru resursă) | branches.name | ex. „ScanExpert Galați” |
| Modality | string | nu | products.modality | |
| ProductId | string UUID sau `""` | nu | appointments.product_id | |
| ProductCode | string | nu | products.barcode | de confirmat că e „Cod” din PixelData |
| ProductName | string | da | appointments.kind / products.name | |
| Laterality | `""`, `"stanga"`, `"dreapta"`, `"bilateral"` | nu | appointments.laterality | |
| Payer | `""`, `"CAS"`, `"Monitor"`, `"Contra cost"`, `"Asigurator privat"` | nu | appointments.payer | CAS/Monitor ⇒ Trimitere |
| Insurer | string | doar dacă Payer = `"Asigurator privat"` | appointments.insurer | |
| ReferralDate | string `YYYY-MM-DD` sau `""` | nu | appointments.referral_date | „Data bilet” |
| ReferralPending | boolean | prezent mereu | referral_due_at nenul | biletul nu a sosit încă |
| ReferralNumber | string | nu | LIPSEȘTE în recepție → `""` în v1 | „Nr./Serie bilet” |
| ReferringDoctorName | string | nu | doctors.full_name | „Medic trimițător” |
| ReferringDoctorClinic | string | nu | doctors.clinic | candidat pentru „Sursa pacient” |
| ReferringDoctorParafa | string | nu | doctors.parafa | |
| PatientId | string UUID | da | customers.id | |
| PatientFullName | string | da | customers.full_name | |
| PatientLastName | string | nu | derivat | `""` ⇒ robotul împarte PatientFullName |
| PatientFirstName | string | nu | derivat | |
| PatientCnp | string 13 cifre sau `""` | nu (robotul îl cere implicit) | customers.cnp | |
| PatientPhone | string E.164 (`+40...`) sau `""` | nu | customer_phones (primar) | |
| PatientBirthDate | string `YYYY-MM-DD` sau `""` | nu | customers.birth_date | |
| PatientSex | string | nu | customers.sex | valori de verificat |
| Notes | string ≤ 2000 caractere | nu | appointments.notes | „Observații” |

## Output item coadă v1 (setat de robot la succes)
| Cheie | Tip | Observații |
|---|---|---|
| OutputSchemaVersion | string `"1"` | |
| Outcome | `"created"` sau `"already_existed"` | `already_existed` = verificarea idempotentă a găsit programarea deja în PixelData |
| PatientCreated | boolean | robotul a creat un pacient nou în PixelData |
| PixelDataResource | string | resursa folosită |
| ProcessedAt | string RFC3339 | |

La eșec, Orchestrator păstrează `ProcessingException`. Mesajul începe cu codul de eroare, de ex. `CNP_INVALID: <mesaj>`.

## Coduri de eroare inițiale
**Business** (problema e în date; fără retry):
- `MISSING_FIELD`, `INVALID_FIELD`
- `UNSUPPORTED_SCHEMA_VERSION`, `UNSUPPORTED_OPERATION`
- `CNP_REQUIRED`, `CNP_INVALID`
- `APPOINTMENT_IN_PAST`
- `RESOURCE_MAPPING_MISSING`, `PATIENT_SOURCE_MAPPING_MISSING`
- `PATIENT_AMBIGUOUS`, `SLOT_OCCUPIED`, `PIXELDATA_CNP_REJECTED`

**System** (problema e în aplicație sau în mediu; retry prin coadă):
- `PIXELDATA_UNAVAILABLE`, `PIXELDATA_LOGIN_FAILED`, `PIXELDATA_UI_TIMEOUT`, `PIXELDATA_SAVE_UNCONFIRMED`

## Fișierul de mapări al robotului (`robot/Data/pixeldata-mappings.json`)
Valorile reale se află din PixelData, pe laptop. Până atunci se pune `"TODO"`, tratat ca valoare lipsă.
```json
{
  "mappingsVersion": "1",
  "initialStatus": "Programat",
  "referralPayers": ["CAS", "Monitor"],
  "resources": [
    { "branchName": "ScanExpert Galați", "modality": "*", "pixelDataResource": "TODO" }
  ],
  "patientSource": {
    "strategy": "referringDoctorClinic",
    "fallback": "TODO",
    "clinicAliases": [ { "clinic": "TODO", "pixelDataSource": "TODO" } ]
  },
  "procedures": [
    { "productCode": "TODO", "pixelDataProcedure": "TODO" }
  ]
}
```
- `resources`:
  - potrivire după `branchName` + `modality` (cu trim, fără diferență între majuscule și minuscule)
  - `modality: "*"` = orice modalitate; potrivirea exactă are prioritate față de `*`
  - lipsă sau `"TODO"` ⇒ `RESOURCE_MAPPING_MISSING`
- `patientSource.strategy`:
  - `"referringDoctorClinic"`: caută clinica medicului în `clinicAliases`; dacă nu o găsește, folosește `fallback`
  - `"fixed"`: folosește mereu `fallback`
  - rezultat `"TODO"` sau gol ⇒ `PATIENT_SOURCE_MAPPING_MISSING`
- `procedures`: `productCode` → denumirea din PixelData; dacă lipsește, se folosește `ProductName`.

## API C# `robot/Logic` (suprafața publică; contract între rpa-engineer și tester)
Constrângeri:
- Limbaj: maximum C# 10, cu namespace-uri bloc. Fără raw string literals, required members, primary constructors, collection expressions sau tipuri `file`.
- Dependențe: doar BCL .NET 8 + System.Text.Json. Fără `using UiPath...`, fără NuGet extern.
- Fără acces la fișiere, rețea sau ceas: metodele primesc string-uri, iar `now` vine ca parametru.
- `CultureInfo.InvariantCulture` peste tot.

```csharp
namespace PixelDataProgramari.Logic
{
    // Proprietăți get; set; pentru fiecare cheie din contract.
    // Tipuri: CreatedAt, ScheduledAt = DateTimeOffset; ScheduledLocalDate = DateTime (doar data);
    // ScheduledLocalTime = TimeSpan; DurationMinutes = int; ReferralPending = bool;
    // ReferralDate, PatientBirthDate = DateTime?; restul string (niciodată null, "" = necunoscut).
    public sealed class AppointmentItem { }

    public static class QueueItemParser
    {
        // in_TransactionItem.SpecificContent în Studio. Valorile pot fi string, bool, long, int, double,
        // DateTime sau obiecte al căror ToString() dă valoarea.
        public static AppointmentItem FromSpecificContent(IDictionary<string, object> specificContent);
        // JSON-ul obiectului SpecificContent (teste, instrumente).
        public static AppointmentItem FromJson(string specificContentJson);
        // Aruncă BusinessRuleViolation: MISSING_FIELD (cheie lipsă), INVALID_FIELD (format greșit),
        // UNSUPPORTED_SCHEMA_VERSION (≠ "1"), UNSUPPORTED_OPERATION (≠ "create").
    }

    public sealed class CnpCheckResult { public bool IsValid { get; } public string Reason { get; } public DateTime? BirthDate { get; } }
    public static class CnpValidator
    {
        public static CnpCheckResult Check(string cnp);
        public static bool IsValid(string cnp);
    }

    public sealed class PersonName { public string LastName { get; } public string FirstName { get; } }
    public static class NameSplitter
    {
        // lastName și firstName ambele ne-goale ⇒ folosite ca atare (trim).
        // Altfel fullName normalizat (spațiile multiple devin unul): primul cuvânt = LastName (Nume), restul = FirstName (Prenume).
        // Un singur cuvânt ⇒ FirstName = "".
        public static PersonName Split(string fullName, string lastName, string firstName);
    }

    public sealed class PixelDataMappings
    {
        public static PixelDataMappings FromJson(string json);
        public string MappingsVersion { get; }
        public string InitialStatus { get; }
        public bool NeedsReferral(string payer);
        public string ResolveResource(string branchName, string modality);
        public string ResolvePatientSource(AppointmentItem item);
        public string ResolveProcedure(string productCode, string productName);
    }

    public sealed class ValidationOptions
    {
        public bool RequireCnp { get; set; } = true;
        public TimeSpan PastTolerance { get; set; } = TimeSpan.Zero;
    }
    public sealed class ValidationIssue { public string Code { get; } public string Field { get; } public string Message { get; } }
    public static class AppointmentValidator
    {
        // Reguli, în ordine:
        // - ProductName, PatientFullName, BranchName goale ⇒ MISSING_FIELD
        // - CNP gol și RequireCnp ⇒ CNP_REQUIRED; CNP prezent și invalid ⇒ CNP_INVALID
        // - ScheduledAt < now - PastTolerance ⇒ APPOINTMENT_IN_PAST
        // - Payer = "Asigurator privat" și Insurer gol ⇒ MISSING_FIELD (Insurer)
        public static IReadOnlyList<ValidationIssue> Validate(AppointmentItem item, DateTimeOffset now, ValidationOptions options);
        // Aruncă BusinessRuleViolation pentru primul issue.
        public static void EnsureValid(AppointmentItem item, DateTimeOffset now, ValidationOptions options);
    }

    public sealed class BusinessRuleViolation : Exception
    {
        // Exception.Message = "<code>: <message>"
        public BusinessRuleViolation(string code, string field, string message);
        public string Code { get; }
        public string Field { get; }
    }

    // Câte un const string pentru fiecare cod de mai sus.
    public static class ErrorCodes
    {
        public static bool IsBusiness(string code);
    }

    public static class TransactionOutput
    {
        public static Dictionary<string, object> Success(string outcome, bool patientCreated, string pixelDataResource, DateTimeOffset processedAt);
    }
}
```
În XAML: `catch BusinessRuleViolation ex` → `throw new BusinessRuleException(ex.Message)`.

## Faze
1. Acum, pe Linux: documentație, contract + exemple, `tools/queue-client` (Go), `robot/Logic` (C#) + teste (rulează pe laptop cu `dotnet test`).
2. Pe laptop: setup Windows + Studio + Orchestrator (docs/06, docs/07), proiect REFramework în Studio, captură UI PixelData, XAML, test end-to-end cu `queue-client`.
3. Faza 2 (mono-ymirr): outbox + dispatcher + callback de status (docs/05).
4. Mai târziu: anulare și mutare; HL7, dacă PixelData permite.
