# Exemple invalide

Fiecare fișier conține DOAR obiectul `SpecificContent` (fără învelișul `{"itemData": ...}`).

Toate pornesc de la același item fictiv și schimbă UN singur lucru:
- pacient `TEST POPESCU ION`, CNP `1850312400012`, telefon `+40700000001`
- programare `2027-03-15 09:30` (`+02:00`), `ScanExpert Galați`, `RMN`, plătitor `CAS`

Verificare:
- `python3 contracts/validate_examples.py`: fișierele din `schema/` trebuie să pice, cele din `business/` trebuie să treacă.
- `dotnet test tests/Logic.Tests`: `BusinessInvalidExamplesTests` verifică codul de eroare al fiecărui fișier din `business/` (pe laptop).

## `schema/`: pică schema

| Fișier | Ce e greșit | Eroare de schemă așteptată |
|---|---|---|
| `missing-appointment-id.json` | lipsește cheia `AppointmentId` | `required` |
| `schema-version-2.json` | `SchemaVersion` = `"2"` | `SchemaVersion` acceptă doar `"1"` |
| `operation-cancel.json` | `Operation` = `"cancel"` | `Operation` acceptă doar `"create"` |
| `appointment-id-not-uuid.json` | `AppointmentId` = `"APPT-2030-0001"` | `AppointmentId` nu e UUID |
| `scheduled-at-without-offset.json` | `ScheduledAt` = `"2030-03-15T09:30:00"` (fără offset) | `ScheduledAt` nu e RFC3339 cu offset |
| `scheduled-local-time-25-00.json` | `ScheduledLocalTime` = `"25:00"` | `ScheduledLocalTime` nu e `HH:MM` valid |
| `duration-minutes-negative.json` | `DurationMinutes` = `-30` | `minimum` 0 |
| `patient-cnp-12-digits.json` | `PatientCnp` = `"185031240001"` (12 cifre) | `PatientCnp` nu are 13 cifre |
| `notes-2001-chars.json` | `Notes` cu 2001 caractere | `maxLength` 2000 |
| `payer-outside-enum.json` | `Payer` = `"Card"` | `enum` |
| `private-insurer-without-insurer.json` | `Payer` = `"Asigurator privat"` și `Insurer` = `""` | `Insurer` obligatoriu la asigurător privat |
| `referral-pending-string.json` | `ReferralPending` = `"false"` (string, nu boolean) | `type` boolean |

## `business/`: trec schema, robotul le respinge

Codul e cel din `ProcessingException` (`<cod>: <mesaj>`), cu `now` = `2026-09-15T08:00:00+03:00` și `ValidationOptions` implicite (`RequireCnp` = true).

| Fișier | Ce e greșit | Cod robot | Cine îl dă |
|---|---|---|---|
| `patient-cnp-wrong-checksum.json` | `PatientCnp` = `"1850312400013"` (cifra de control corectă e `2`) | `CNP_INVALID` | `AppointmentValidator` |
| `patient-cnp-invalid-month.json` | `PatientCnp` = `"1851312400013"`: cifra de control corectă, luna `13` | `CNP_INVALID` | `AppointmentValidator` |
| `patient-cnp-empty.json` | `PatientCnp` = `""` | `CNP_REQUIRED` | `AppointmentValidator` |
| `branch-name-empty.json` | `BranchName` = `""` (și `BranchId` = `""`) | `MISSING_FIELD` (câmp `BranchName`) | `AppointmentValidator` |
| `appointment-in-past.json` | programare pe `2020-01-15 09:30` | `APPOINTMENT_IN_PAST` | `AppointmentValidator` |
