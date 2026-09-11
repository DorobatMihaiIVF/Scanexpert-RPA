# 04 — Maparea câmpurilor

Fiecare câmp din fișa PixelData ([flux](sources/flux-programare-pixeldata.original.md), pașii robotului în [02](02-flux-pixeldata.md)) ↔ cheia din contract ([03](03-contract-coada.md)) ↔ sursa din recepție. Sursa: investigatie §2 „Tabele” și „Corespondență PixelData ↔ recepție”; decizii „Contract item coadă v1” și „Fișierul de mapări al robotului”.

| Status | Sens |
|---|---|
| Există | recepția are valoarea direct |
| Parțial | recepția o are incomplet sau opțional |
| Lipsește | recepția nu o are |
| Ambiguu | sensul câmpului PixelData nu e clar |
| Derivat | se calculează din alte valori |

„Mapări” = `robot/Data/pixeldata-mappings.json`. „TODO pe laptop” = se află din PixelData, în faza 2.

## 1. Câmpurile din PixelData

| # | Câmp PixelData | Cheie contract | Sursă în recepție | Status | Cum tratează robotul |
|---|---|---|---|---|---|
| 1 | Data (mini-calendar, PROGRAMĂRI) | `ScheduledLocalDate` | `appointments.scheduled_at`, în Europe/Bucharest | Derivat | selectează ziua |
| 2 | Resursă (drop-down lângă calendar, ex. „C.A.L.A.T.I.” în clip) | `BranchName` + `Modality` | `branches.name`, `products.modality` | Lipsește | `resources` în mapări; lipsă sau `"TODO"` ⇒ `RESOURCE_MAPPING_MISSING`; „C.A.L.A.T.I.” = Galați e o presupunere neverificată |
| 3 | Ora (slot în grilă) | `ScheduledLocalTime` | `appointments.scheduled_at`, în Europe/Bucharest | Derivat | deschide fișa pe rândul orei; ocupat ⇒ `SLOT_OCCUPIED` |
| 4 | Nume/CNP (căutare) | `PatientCnp`, `PatientFullName` | `customers.cnp`, `customers.full_name` | Parțial | caută pacientul după CNP/nume (nu există flag „pacient nou”); rezultat neunic ⇒ `PATIENT_AMBIGUOUS`; niciun rezultat ⇒ pacient nou |
| 5 | Date demografice: Nume | `PatientLastName` | derivat din `customers.full_name` (un singur câmp) | Parțial | `""` ⇒ primul cuvânt din `PatientFullName` (aceeași euristică ca R/web/js/booking-name.js) |
| 6 | Date demografice: Prenume | `PatientFirstName` | derivat din `customers.full_name` | Parțial | restul cuvintelor; un singur cuvânt ⇒ `""` |
| 7 | Date demografice: CNP | `PatientCnp` | `customers.cnp` (opțional, unic când nu e gol) | Parțial | gol ⇒ `CNP_REQUIRED` (implicit); invalid ⇒ `CNP_INVALID`; alertă „Eroare CNP pacient!” ⇒ `PIXELDATA_CNP_REJECTED` |
| 8 | Contact (coloană în grilă) | `PatientPhone` | `customer_phones.phone_e164`, numărul primar | Există | câmpul de introducere: TODO pe laptop |
| 9 | Trimitere (checkbox) | derivat din `Payer` | `appointments.payer` | Derivat | bifat dacă `Payer` ∈ `referralPayers` (`CAS`, `Monitor`) |
| 10 | Nr./Serie bilet | `ReferralNumber` | lipsește; există doar `waiting_list.referral_series` | Lipsește | `""` în v1; câmpul rămâne gol (obligatoriu în PixelData? C5) |
| 11 | Data bilet | `ReferralDate` | `appointments.referral_date` | Există | completat când nu e `""` |
| 12 | Sursa pacient | derivat | `doctors.clinic` (`ReferringDoctorClinic`) sau `appointments.source` | Ambiguu | `patientSource` în mapări; rezultat gol sau `"TODO"` ⇒ `PATIENT_SOURCE_MAPPING_MISSING` |
| 13 | Medic trimițător | `ReferringDoctorName` | `appointments.referring_doctor_id` → `doctors.full_name` | Parțial | ales din listă; gol ⇒ câmpul rămâne necompletat; ne-gol și absent din lista PixelData ⇒ `REFERRING_DOCTOR_NOT_FOUND` (C11) |
| 14 | Stare | — | `appointments.status` | Derivat | `initialStatus` din mapări (`"Programat"`); fără stare PixelData blochează salvarea („Nu ați selectat starea pacientului!”) |
| 15 | Proceduri selectate: Cod | `ProductCode` | `products.barcode` (recepția nu îl expune) | Parțial | `procedures`: `productCode` → procedura PixelData |
| 16 | Proceduri selectate: Denumire | `ProductName` | `products.name`, altfel `appointments.kind` | Există | folosită când `procedures` nu are codul; procedura rezolvată absentă din lista PixelData ⇒ `PROCEDURE_NOT_FOUND` |
| 17 | Proceduri selectate: Cantitate | — | 1 produs pe programare | Derivat | mereu 1 |
| 18 | Proceduri selectate: Preț | — | prețurile pe contract sunt în PixelData (B/norn/plans/clinic_operations.py:319) | Lipsește | nu se completează |
| 19 | Observații | `Notes` | `appointments.notes` (≤ 2000) | Există | câmpul din fișă: TODO pe laptop |

## 2. Chei fără câmp PixelData în flux

| Cheie | Sursă în recepție | Status | Folosire în robot |
|---|---|---|---|
| `SchemaVersion` | constant `"1"` | Derivat | ≠ `"1"` ⇒ `UNSUPPORTED_SCHEMA_VERSION` |
| `Operation` | constant `"create"` | Derivat | ≠ `"create"` ⇒ `UNSUPPORTED_OPERATION` |
| `AppointmentId` | `appointments.id` | Există | `Reference` = `create-<AppointmentId>` |
| `CreatedAt` | `appointments.created_at` | Există | informativ |
| `Source` | `appointments.source` | Există | informativ; nu e „Sursa pacient” |
| `ScheduledAt` | `appointments.scheduled_at` | Există | trecut ⇒ `APPOINTMENT_IN_PAST` |
| `DurationMinutes` | `products.duration_minutes` | Parțial | `0` fără produs; câmp PixelData necunoscut (de verificat) |
| `BranchId` | `appointments.branch_id` | Există | informativ |
| `ProductId` | `appointments.product_id` | Parțial | informativ |
| `Laterality` | `appointments.laterality` | Există | câmp PixelData necunoscut (C12) |
| `Payer` | `appointments.payer` | Există | derivă „Trimitere” |
| `Insurer` | `appointments.insurer` | Există | gol la `Asigurator privat` ⇒ `MISSING_FIELD`; câmp PixelData necunoscut (C12) |
| `ReferralPending` | `appointments.referral_due_at` nenul | Derivat | tratare nedefinită (§3, C10) |
| `ReferringDoctorClinic` | `doctors.clinic` | Parțial | intrare pentru `patientSource` |
| `ReferringDoctorParafa` | `doctors.parafa` | Parțial | poate deosebi medici cu același nume (de verificat) |
| `PatientId` | `customers.id` | Există | informativ |
| `PatientBirthDate` | `customers.birth_date` | Parțial | fără câmp în flux; CNP-ul conține data nașterii |
| `PatientSex` | `customers.sex` | Parțial | valori de verificat (recepția: `''`, `M`, `F`); fără câmp în flux |

## 3. Goluri

| # | Gol | Propunere | Decide |
|---|---|---|---|
| G1 | Resursa PixelData nu există în recepție | `resources` în mapări (`branchName` + `modality`, `"*"` = orice modalitate), completat pe laptop; locul în faza 3: [05](05-integrare-receptie.md) | Clinică (valori, C1); Echipa ScanExpert (loc, S1) |
| G2 | Nr./Serie bilet lipsește | v1 `""`; faza 3: coloană nouă + sursa valorii | Clinică (C5); Echipa ScanExpert (S2) |
| G3 | „Sursa pacient” ambiguă | `strategy` `"referringDoctorClinic"` cu `clinicAliases` + `fallback`, sau `"fixed"` | Clinică (C2) |
| G4 | „Stare” inițială presupusă | `initialStatus` = `"Programat"` până la confirmare | Clinică (C3) |
| G5 | „Cod” PixelData vs `products.barcode` | `procedures`; fără potrivire se folosește `ProductName` | Clinică (C4); PixelData SRL |
| G6 | CNP opțional în recepție | robotul cere CNP implicit (`CNP_REQUIRED`); fluxul pentru pacienți fără CNP e nedecis | Clinică (C6) |
| G7 | Nume și prenume într-un singur `full_name` | euristica „primul cuvânt = Nume”; greșește la nume compuse | Echipa ScanExpert (S9) |
| G8 | `ReferralPending` = `true`: ce se pune la „Trimitere” și bilet; auto-anularea BR-08 după 2h poate lăsa o programare fantomă în PixelData | de decis înainte de faza 3 ([05](05-integrare-receptie.md)) | Clinică (C10); Echipa ScanExpert |
| G9 | Medic trimițător absent din lista PixelData | eșec Business `REFERRING_DOCTOR_NOT_FOUND` (2026-09-11); `ReferringDoctorName` gol lasă câmpul necompletat | Clinică (C11: cine adaugă medicul în PixelData) |
| G10 | Lateralitate, asigurător privat: niciun câmp în flux | de aflat pe laptop; altfel în Observații | Clinică (C12) |
| G11 | O programare = un produs; PixelData permite mai multe proceduri | v1 adaugă o singură procedură | Clinică (C7) |
| G12 | Câmpurile pentru telefon și observații din fișa PixelData | TODO pe laptop | Dezvoltator RPA |
| G13 | Medicii și procedurile din recepție pot lipsi din listele PixelData | listele PixelData rămân sursa: valoare absentă ⇒ eșec Business (`REFERRING_DOCTOR_NOT_FOUND`, `PROCEDURE_NOT_FOUND`), fără retry; cine le ține sincronizate: clinica (de verificat) | Clinică (C4, C11) |

## 4. Starea programării: recepție → PixelData

Recepția are `programat` / `confirmat` / `necesita_contact` / `anulat`; PixelData are „În lucru”, „Programat” și altele (investigatie §2). Contractul v1 nu poartă starea: la creare robotul folosește mereu `initialStatus` din mapări.

| `appointments.status` | Sens în recepție | Stare PixelData | În v1 |
|---|---|---|---|
| `programat` | creată; și după mutarea orei | `initialStatus` = `"Programat"` (de confirmat, C3) | da, la creare |
| `confirmat` | confirmată, cel mult cu 1 zi înainte | de verificat | nu; `update` în v2 |
| `necesita_contact` | pacientul trebuie contactat | de verificat | nu |
| `anulat` | anulată, inclusiv auto-anularea BR-08 | de verificat | nu; `cancel` în v2 |
