# Workflow-urile procesului `PixelDataProgramari`

Ce se construiește în Studio, pe laptop: fiecare workflow, calea lui în proiectul REFramework, argumentele, pașii, elementele de capturat în Object Repository și codul de eroare pe care îl aruncă fiecare eșec.

Pașii în termeni de business: [docs/02-flux-pixeldata.md](../docs/02-flux-pixeldata.md). Cum se creează proiectul și cum se capturează interfața: [docs/08-construire-in-studio.md](../docs/08-construire-in-studio.md). Codurile de eroare: [docs/03-contract-coada.md](../docs/03-contract-coada.md) §6. Câmp PixelData ↔ cheie din contract: [docs/04-mapare-campuri.md](../docs/04-mapare-campuri.md).

Nimic din acest fișier nu a fost rulat: PixelData nu există pe Linux. Numele ecranelor și ale elementelor sunt propuneri; ce nu poate fi verificat aici e marcat „(de verificat)”.

## 1. Structura proiectului

```text
PixelDataProgramari/
├── Main.xaml                          din template; două modificări (§3)
├── Config.xlsx                        rândurile din robot/Config.rows.csv
├── project.json                       generat de Studio
├── Data/
│   └── pixeldata-mappings.json        copiat din robot/Data/
├── Logic/                             copiat din robot/Logic/, compilat de Studio
├── Framework/
│   ├── InitAllSettings.xaml           din template, nemodificat
│   ├── InitAllApplications.xaml       §4.1
│   ├── GetTransactionData.xaml        din template, nemodificat
│   ├── Process.xaml                   §5
│   ├── SetTransactionStatus.xaml      §4.4
│   ├── CloseAllApplications.xaml      §4.2
│   ├── KillAllProcesses.xaml          §4.3
│   └── TakeScreenshot.xaml            din template, nemodificat
└── PixelData/                         folder nou, workflow-urile proprii (§6)
    ├── Login.xaml
    ├── CheckExistingAppointment.xaml
    ├── OpenProgramari.xaml
    ├── SelectDateAndResource.xaml
    ├── SelectSlot.xaml
    ├── FindOrCreatePatient.xaml
    ├── FillPatientAndReferral.xaml
    ├── SelectPatientSourceAndDoctor.xaml
    ├── SetStatus.xaml
    ├── AddProcedures.xaml
    ├── Save.xaml
    ├── HandlePopups.xaml
    └── VerifySaved.xaml
```

## 2. Convenții care se aplică peste tot

| Convenție | Detaliu |
|---|---|
| Expresii | C#. Accesul în `Config` se scrie cu paranteze drepte: `in_Config["UiElementTimeoutSeconds"]` |
| Tipuri din `Config` | Excel întoarce `Object`. Numere: `Convert.ToInt32(in_Config["..."])`. Boolean: `Convert.ToBoolean(in_Config["..."])`. Text: `in_Config["..."].ToString()` |
| Fereastra PixelData | fiecare workflow care atinge interfața are propriul **Use Application/Browser** pe fereastra principală, cu **Open = Never** și **Close = Never**. Aplicația e pornită o singură dată, în `Login.xaml`, și închisă o singură dată, în `CloseAllApplications.xaml` |
| Timeout | din `Config`: `UiElementTimeoutSeconds` pentru interacțiuni, `PopupCheckTimeoutSeconds` pentru Check App State, `SaveConfirmTimeoutSeconds` pentru confirmarea salvării, `PixelDataStartTimeoutSeconds` la pornire. Activitățile moderne cer secunde, cele clasice milisecunde (de verificat) |
| Metodă de input | **Simulate** implicit. Controalele Delphi pot să nu răspundă la Simulate; atunci **SendWindowMessages**, iar în ultimă instanță **HardwareEvents** (de verificat pe laptop, control cu control) |
| Țintire | selector Strict/Fuzzy cu ancoră; Computer Vision doar unde selectorul nu prinde celula; Image ultima variantă |
| Erori de business | se aruncă din workflow ca `PixelDataProgramari.Logic.BusinessRuleViolation`, cu unul dintre codurile din `ErrorCodes`. Conversia în excepția UiPath se face O SINGURĂ dată, în `Process.xaml` (§5) |
| Erori de sistem | `throw new ApplicationException("<COD>: <mesaj>")`. REFramework tratează orice nu e `BusinessRuleException` ca eroare de sistem, deci itemul e reîncercat de coadă |
| Mesaje | în română, fără date ale pacientului: mesajul ajunge în `ProcessingException`, care e vizibil în Orchestrator |
| Elementele UI | din Object Repository, niciodată selector scris în activitate |

Numele de ecrane și de elemente din tabele sunt cele propuse pentru Object Repository (§7).

## 3. Modificările în `Main.xaml`

Template-ul se schimbă în două locuri, altfel rămâne cum e generat.

| # | Unde | Modificare | De ce |
|---|---|---|---|
| 1 | variabile | variabilă nouă `TransactionOutput` de tip `Dictionary<String,Object>` | poartă Output-ul de la `Process.xaml` la `SetTransactionStatus.xaml` |
| 2 | starea Process | invocarea `Process.xaml` primește argumentul nou `out_TransactionOutput` → `TransactionOutput`; invocarea `SetTransactionStatus.xaml` primește `in_TransactionOutput` → `TransactionOutput` | Output-ul se setează pe item doar la succes |

`MaxRetryNumber` rămâne `0`: retry-ul îl face coada (`robot/Config.rows.csv`).

## 4. Workflow-urile din `Framework`

### 4.1 `Framework\InitAllApplications.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Config` | In | `Dictionary<String,Object>` |

Pași:

1. **Invoke Workflow File** `Framework\KillAllProcesses.xaml` (`in_Config`) — închide o instanță rămasă din sesiunea robotului, înainte de a porni una nouă.
2. **Read Text File**: `System.IO.Path.Combine(Environment.CurrentDirectory, in_Config["PixelDataMappingsPath"].ToString())` → `MappingsJson`.
3. **Assign**: `in_Config["PixelDataMappings"] = PixelDataProgramari.Logic.PixelDataMappings.FromJson(MappingsJson)` — `Config` e `Dictionary<String,Object>`, deci poate ține obiectul; așa ajunge în `Process.xaml` fără argumente noi.
4. **Get Credential** (Orchestrator), `AssetName` = `in_Config["PixelDataCredentialAsset"].ToString()` → `Username` (String) și `Password` (SecureString).
5. **Invoke Workflow File** `PixelData\Login.xaml` cu `in_ExePath`, `in_Username`, `in_Password`, `in_StartTimeoutSeconds`.

| Eșec | Cod | Tip |
|---|---|---|
| fișierul de mapări lipsește sau e greșit (`FormatException` din `PixelDataMappings.FromJson`) | se lasă să iasă ca atare | eroare de inițializare: REFramework oprește procesul, nu itemul. E o eroare de configurare, deci nu primește cod de business |
| asset-ul nu există sau robotul nu are drept pe folder | se lasă să iasă | idem |
| restul | vezi `Login.xaml` (§6.1) | |

Parola nu se scrie niciodată în `Config`, în log sau într-o variabilă de tip String.

### 4.2 `Framework\CloseAllApplications.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Config` | In | `Dictionary<String,Object>` |

Pași:

1. **Use Application/Browser** pe `PixelData > Principal > FereastraPrincipala`, Open = Never, Close = Never.
2. Închidere pe drumul aplicației (meniul propriu sau butonul de închidere al ferestrei — elementul exact, de verificat pe laptop).
3. **Check App State** pe `FereastraPrincipala`, ramura „nu apare”, timeout `UiElementTimeoutSeconds`.
4. Dacă fereastra e încă acolo: **Invoke Workflow File** `Framework\KillAllProcesses.xaml`.

Nu aruncă niciodată. REFramework îl apelează în `Try`, iar pe `Catch` trece la `KillAllProcesses.xaml`; un workflow de închidere care aruncă ascunde eroarea reală a itemului.

### 4.3 `Framework\KillAllProcesses.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Config` | In | `Dictionary<String,Object>` |

Pași:

1. **Assign** `ProcessName` = `System.IO.Path.GetFileNameWithoutExtension(in_Config["PixelDataExePath"].ToString())`.
2. **Assign** `CurrentSession` = `System.Diagnostics.Process.GetCurrentProcess().SessionId`.
3. **For Each** `p` în `System.Diagnostics.Process.GetProcessesByName(ProcessName)`, tip argument `System.Diagnostics.Process`:
   - **If** `p.SessionId == CurrentSession` → **Invoke Method** `p.Kill()`, apoi `p.WaitForExit(5000)`.
   - **Else** → **Log Message** (Info): „proces PixelData în altă sesiune Windows, lăsat neatins”.
4. Fiecare `Kill` într-un `Try/Catch` pe `System.Exception`, cu Log Message: procesul poate dispărea între enumerare și `Kill`.

**Nu se folosește activitatea Kill Process cu numele procesului.** Laptopul e folosit și de oameni: un `Kill` după nume poate închide PixelData în sesiunea unui operator, în mijlocul lucrului lui. Filtrul pe `SessionId` e singura garanție că robotul atinge doar ce a pornit el (investigatie §4 „PixelData deja deschis”). Testul E18 din [docs/10-plan-teste.md](../docs/10-plan-teste.md) exact asta verifică.

Nu aruncă.

### 4.4 `Framework\SetTransactionStatus.xaml`

Din template, cu un argument nou.

| Argument | Direcție | Tip | Notă |
|---|---|---|---|
| `in_TransactionOutput` | In | `Dictionary<String,Object>` | nou |

Pe ramura de succes, proprietatea **Output** a activității **Set Transaction Status** primește `in_TransactionOutput`. Restul (business vs system, retry, loguri) rămâne din template.

## 5. `Framework\Process.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_TransactionItem` | In | `UiPath.Core.QueueItem` |
| `in_Config` | In | `Dictionary<String,Object>` |
| `out_TransactionOutput` | Out | `Dictionary<String,Object>` |

Variabile locale: `Item` (`AppointmentItem`), `Mappings` (`PixelDataMappings`), `Name` (`PersonName`), `Resource`, `PatientSource`, `Procedure` (String), `Options` (`ValidationOptions`), `AlreadyExists`, `PatientCreated` (Boolean), `Outcome` (String).

Tot corpul stă într-un **Try Catch**:

```
catch BusinessRuleViolation ex  ⇒  throw new BusinessRuleException(ex.Message)
```

Acesta e SINGURUL loc unde se face conversia. `Main.xaml` recunoaște doar `BusinessRuleException`; o `BusinessRuleViolation` scăpată până acolo ar fi tratată ca eroare de sistem și itemul ar fi reîncercat degeaba, cu aceleași date greșite.

Pași:

| # | Pas | Cum | Cod la eșec |
|---|---|---|---|
| 1 | parsare | `Item = QueueItemParser.FromSpecificContent(in_TransactionItem.SpecificContent)` | `MISSING_FIELD`, `INVALID_FIELD`, `UNSUPPORTED_SCHEMA_VERSION`, `UNSUPPORTED_OPERATION` |
| 2 | opțiuni | `Options = new ValidationOptions { RequireCnp = Convert.ToBoolean(in_Config["RequireCnp"]), PastTolerance = TimeSpan.FromMinutes(Convert.ToInt32(in_Config["PastToleranceMinutes"])) }` | — |
| 3 | validare | `AppointmentValidator.EnsureValid(Item, DateTimeOffset.Now, Options)` | `MISSING_FIELD`, `CNP_REQUIRED`, `CNP_INVALID`, `APPOINTMENT_IN_PAST` |
| 4 | mapări | `Mappings = (PixelDataMappings)in_Config["PixelDataMappings"]` | — |
| 5 | resursă | `Resource = Mappings.ResolveResource(Item.BranchName, Item.Modality)` | `RESOURCE_MAPPING_MISSING` |
| 6 | sursa pacientului | `PatientSource = Mappings.ResolvePatientSource(Item)` | `PATIENT_SOURCE_MAPPING_MISSING` |
| 7 | procedură | `Procedure = Mappings.ResolveProcedure(Item.ProductCode, Item.ProductName)` | — (nu aruncă; lipsa în PixelData se vede la §6.10) |
| 8 | nume | `Name = NameSplitter.Split(Item.PatientFullName, Item.PatientLastName, Item.PatientFirstName)` | — |
| 9 | `OpenProgramari.xaml` | §6.3 | `PIXELDATA_UI_TIMEOUT` |
| 10 | `SelectDateAndResource.xaml` | §6.4, cu `Item.ScheduledLocalDate` și `Resource` | `PIXELDATA_UI_TIMEOUT`, `RESOURCE_MAPPING_MISSING` |
| 11 | `CheckExistingAppointment.xaml` | §6.2 → `AlreadyExists` | `PIXELDATA_UI_TIMEOUT` |
| 12 | **If** `AlreadyExists` | `Outcome = TransactionOutput.OutcomeAlreadyExisted`, `PatientCreated = false`, se sar pașii 13–20 | — |
| 13 | `SelectSlot.xaml` | §6.5 | `SLOT_OCCUPIED`, `PIXELDATA_UI_TIMEOUT` |
| 14 | `FindOrCreatePatient.xaml` | §6.6 → `PatientCreated` | `PATIENT_AMBIGUOUS`, `PIXELDATA_CNP_REJECTED`, `PIXELDATA_UI_TIMEOUT` |
| 15 | `FillPatientAndReferral.xaml` | §6.7, cu `Mappings.NeedsReferral(Item.Payer)` | `PIXELDATA_UI_TIMEOUT` |
| 16 | `SelectPatientSourceAndDoctor.xaml` | §6.8, cu `PatientSource` și `Item.ReferringDoctorName` | `REFERRING_DOCTOR_NOT_FOUND`, `PATIENT_SOURCE_MAPPING_MISSING`, `PIXELDATA_UI_TIMEOUT` |
| 17 | `SetStatus.xaml` | §6.9, cu `Mappings.InitialStatus` | `PIXELDATA_UI_TIMEOUT` |
| 18 | `AddProcedures.xaml` | §6.10, cu `Procedure` | `PROCEDURE_NOT_FOUND`, `PIXELDATA_UI_TIMEOUT` |
| 19 | `Save.xaml` | §6.11 (apelează `HandlePopups.xaml`) | `PIXELDATA_CNP_REJECTED`, `PIXELDATA_SAVE_UNCONFIRMED`, `PIXELDATA_UI_TIMEOUT` |
| 20 | `VerifySaved.xaml` | §6.13 | `PIXELDATA_SAVE_UNCONFIRMED` |
| 21 | Output | `out_TransactionOutput = TransactionOutput.Success(Outcome, PatientCreated, Resource, DateTimeOffset.Now)` | — |

La pasul 12, `Outcome` este `TransactionOutput.OutcomeCreated` pe ramura normală.

Ordinea contează în două locuri. Validarea (pașii 1–8) se face înainte de orice atingere a interfeței: un item greșit nu deschide PixelData. Verificarea de idempotență (pasul 11) se face înainte de deschiderea fișei: un job căzut la salvare a lăsat deja programarea în PixelData, iar retry-ul trebuie să o găsească, nu să o creeze a doua oară (decizia D4 din [docs/01-arhitectura.md](../docs/01-arhitectura.md)).

## 6. Workflow-urile din `PixelData`

### 6.1 `Login.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_ExePath` | In | `String` |
| `in_Username` | In | `String` |
| `in_Password` | In | `SecureString` |
| `in_StartTimeoutSeconds` | In | `Int32` |

Pași:

1. **Use Application/Browser**, File path = `in_ExePath`, Open = **If not open**, Close = **Never**, timeout = `in_StartTimeoutSeconds`.
2. **Check App State** pe `Login > FereastraLogin`, ramura „apare”, timeout = `in_StartTimeoutSeconds`.
3. **Type Into** `Login > CampUtilizator` cu `in_Username`.
4. **Type Secure Text** `Login > CampParola` cu `in_Password`.
5. **Click** `Login > ButonOk`.
6. **Check App State** pe `Principal > FereastraPrincipala`, ramura „apare”, timeout = `in_StartTimeoutSeconds`.
7. Pe ramura „nu apare”: **Check App State** pe `Login > MesajEroareLogin` ca să deosebești parola greșită de o pornire lentă.

| Eșec | Cod | Tip |
|---|---|---|
| executabilul nu pornește, fereastra de login nu apare | `PIXELDATA_UNAVAILABLE` | system |
| apare `MesajEroareLogin`, sau fereastra de login rămâne pe ecran după Ok | `PIXELDATA_LOGIN_FAILED` | system |

Atenție la repetarea automată: PixelData poate bloca contul după câteva încercări greșite (de verificat). Coada reîncearcă o singură dată (`Auto retry` maximum 1).

### 6.2 `CheckExistingAppointment.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Item` | In | `AppointmentItem` |
| `in_Resource` | In | `String` |
| `in_Config` | In | `Dictionary<String,Object>` |
| `out_Exists` | Out | `Boolean` |

Ziua și resursa sunt deja selectate (pasul 10 din §5), deci grila afișată e exact cea căutată.

1. **Extract Table Data** pe `Programari > GrilaProgramari` → `DataTable` cu coloanele `Ora`, `Nume`, `Observatii`, `Contact`, `Serviciu`.
2. **Assign** `out_Exists` = există un rând cu `Ora` = `in_Item.ScheduledLocalTime.ToString(@"hh\:mm")` și `Nume` care conține numele pacientului (comparație fără diferență de majuscule, după `Trim`).
3. Dacă grila nu se poate extrage, se cade pe `Get Text` peste rândul orei (de verificat care variantă merge).

Identitatea programării e CNP + dată + oră + resursă (D4). Data și resursa sunt fixate de ecran; ora e cheia rândului. CNP-ul **nu apare în grilă**, deci se compară numele — de verificat pe laptop dacă grila are o coloană cu CNP-ul sau dacă fișa trebuie deschisă ca să îl citești. Până atunci, potrivirea pe nume + oră e mai slabă decât cere D4 și poate da un fals `already_existed` pentru doi pacienți cu același nume la aceeași oră (practic imposibil, fiindcă un slot ține un pacient) sau un fals negativ dacă PixelData afișează numele altfel decât recepția.

| Eșec | Cod | Tip |
|---|---|---|
| grila nu apare, extragerea nu răspunde | `PIXELDATA_UI_TIMEOUT` | system |

### 6.3 `OpenProgramari.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Config` | In | `Dictionary<String,Object>` |

1. **Click** `Principal > MeniuProgramari` (meniul vertical din stânga, eticheta `PROGRAMĂRI`).
2. **Check App State** pe `Programari > MiniCalendar`, ramura „apare”.

| Eșec | Cod | Tip |
|---|---|---|
| calendarul nu apare în `UiElementTimeoutSeconds` | `PIXELDATA_UI_TIMEOUT` | system |

### 6.4 `SelectDateAndResource.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Date` | In | `DateTime` |
| `in_Resource` | In | `String` |
| `in_Config` | In | `Dictionary<String,Object>` |

1. Navigare pe lună în `Programari > MiniCalendar`: citește `AntetLuna` cu **Get Text**, apoi **Click** pe `ButonLunaUrmatoare` / `ButonLunaAnterioara` până ajungi la luna lui `in_Date`. Numărul de click-uri se calculează din diferența de luni, nu se repetă la nesfârșit.
2. **Click** pe `MiniCalendarZi` cu ziua `in_Date.Day` — element cu selector parametrizat (§7).
3. **Select Item** pe `Programari > DropdownResursa` cu `in_Resource`.
4. **Get Text** pe `DropdownResursa` și compară cu `in_Resource` (după `Trim`, fără diferență de majuscule).

| Eșec | Cod | Tip |
|---|---|---|
| calendarul sau dropdown-ul nu răspund | `PIXELDATA_UI_TIMEOUT` | system |
| `in_Resource` nu există în listă | `RESOURCE_MAPPING_MISSING` | business |

Al doilea caz reutilizează deliberat codul pentru „nu există mapare”: în ambele situații `pixeldata-mappings.json` nu spune adevărul despre PixelData, iar remediul e același — corectarea fișierului. Dacă la teste iese că cele două cazuri trebuie deosebite, se adaugă un cod nou, exact ca `PROCEDURE_NOT_FOUND` (de verificat).

### 6.5 `SelectSlot.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Time` | In | `TimeSpan` |
| `in_Config` | In | `Dictionary<String,Object>` |

Trei strategii, în ordinea preferinței; prima care merge pe laptop rămâne, restul se șterg (de verificat care):

1. **Extract Table Data** pe `GrilaProgramari` → găsește rândul cu `Ora` = `in_Time.ToString(@"hh\:mm")`; dacă `Nume` e ne-gol, slotul e ocupat. **Double Click** pe `Programari > CelulaOra` cu ora ca parametru.
2. Computer Vision sau **Find Text** cu ancoră pe textul orei, apoi **Click** cu `ClickType = Double`.
3. Tastatură: click pe primul rând al grilei, apoi **Send Hotkey** `down` de `n` ori și `enter`. `n` se calculează din ora de început a grilei și pasul de slot — ambele de aflat pe laptop. Varianta cea mai fragilă; se folosește doar dacă primele două nu merg.

După deschidere: **Check App State** pe `Fisa > TabPacientSiDocumente`, ramura „apare”.

| Eșec | Cod | Tip |
|---|---|---|
| rândul orei are deja un pacient | `SLOT_OCCUPIED` | business |
| rândul orei nu există în grilă (ora nu e în programul resursei) | `SLOT_OCCUPIED` | business (de verificat dacă merită un cod propriu) |
| fișa nu se deschide | `PIXELDATA_UI_TIMEOUT` | system |

Rezoluția și scalarea trebuie să fie identice la captură și la rulare, altfel strategiile 2 și 3 nimeresc alt rând ([docs/07-setup-laptop.md](../docs/07-setup-laptop.md) §4).

### 6.6 `FindOrCreatePatient.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Item` | In | `AppointmentItem` |
| `in_Name` | In | `PersonName` |
| `in_Config` | In | `Dictionary<String,Object>` |
| `out_PatientCreated` | Out | `Boolean` |

1. **Type Into** `Fisa > CampNumeCnp` cu `in_Item.PatientCnp` dacă e ne-gol, altfel cu `in_Item.PatientFullName`, urmat de `Enter`.
2. **Check App State** pe lista de rezultate (`Fisa > ListaRezultatePacienti`, de verificat dacă există ca listă sau ca populare directă a câmpurilor).
3. Un singur rezultat ⇒ selectare, `out_PatientCreated = false`.
4. Niciun rezultat ⇒ pacient nou:
   - **Click** `Fisa > MeniuDateDemografice` (meniul din stânga fișei).
   - **Type Into** `DateDemografice > CampNume` cu `in_Name.LastName`, `CampPrenume` cu `in_Name.FirstName`, `CampCnp` cu `in_Item.PatientCnp`.
   - **Invoke Workflow File** `PixelData\HandlePopups.xaml` — dacă PixelData refuză CNP-ul, apare `Eroare` / „Eroare CNP pacient! CNP incorect. Căutarea returnează eroare”.
   - salvare pacient (`DateDemografice > ButonSalveazaPacient`, de verificat numele butonului), apoi `out_PatientCreated = true`.
5. Mai multe rezultate ⇒ business.

| Eșec | Cod | Tip |
|---|---|---|
| căutarea întoarce mai mulți pacienți | `PATIENT_AMBIGUOUS` | business |
| popup „Eroare CNP pacient!” | `PIXELDATA_CNP_REJECTED` | business |
| ecranul nu răspunde | `PIXELDATA_UI_TIMEOUT` | system |

`in_Name` vine din `NameSplitter.Split`, nu din câmpurile brute: recepția ține un singur `full_name`, iar împărțirea „primul cuvânt = Nume” greșește la nume compuse (gol G7 în [docs/04-mapare-campuri.md](../docs/04-mapare-campuri.md)). Un pacient creat greșit rămâne greșit în PixelData, deci pasul 5 e preferabil unei potriviri aproximative.

### 6.7 `FillPatientAndReferral.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Item` | In | `AppointmentItem` |
| `in_NeedsReferral` | In | `Boolean` |
| `in_Config` | In | `Dictionary<String,Object>` |

1. **If** `in_NeedsReferral` → **Check/Uncheck** pe `Fisa > CheckboxTrimitere` (Check). Altfel nu se atinge.
2. **If** `in_NeedsReferral` și `in_Item.ReferralNumber` ne-gol → **Type Into** `Fisa > CampNrSerieBilet`. În v1 cheia e mereu `""`, deci pasul nu rulează (gol G2).
3. **If** `in_Item.ReferralDate.HasValue` → **Type Into** `Fisa > CampDataBilet` cu `in_Item.ReferralDate.Value.ToString("dd.MM.yyyy")` — formatul de dată al PixelData e de verificat pe laptop; dacă e alt format, se schimbă aici, într-un singur loc.
4. **If** `in_Item.PatientPhone` ne-gol → **Type Into** `Fisa > CampContact` (câmpul din fișă e de aflat pe laptop; în grilă coloana se numește `Contact`).
5. **If** `in_Item.Notes` ne-gol → **Type Into** `Fisa > CampObservatii`.

| Eșec | Cod | Tip |
|---|---|---|
| un câmp nu apare sau nu primește textul | `PIXELDATA_UI_TIMEOUT` | system |

`ReferralPending = true` (biletul nu a sosit încă) nu are tratare decisă: v1 completează doar ce e prezent. `Laterality`, `Insurer` și `DurationMinutes` nu au câmp cunoscut în fișă; nu se scriu nicăieri în v1 (golurile G8 și G10).

### 6.8 `SelectPatientSourceAndDoctor.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_PatientSource` | In | `String` |
| `in_ReferringDoctorName` | In | `String` |
| `in_Config` | In | `Dictionary<String,Object>` |

1. **Select Item** pe `Fisa > DropdownSursaPacient` cu `in_PatientSource`; **Get Text** pentru confirmare.
2. **If** `in_ReferringDoctorName` e gol → nu se atinge `DropdownMedicTrimitator`; workflow-ul se termină. Un nume gol **nu** este eroare: recepția nu cere medic trimițător.
3. Altfel **Select Item** pe `Fisa > DropdownMedicTrimitator` cu `in_ReferringDoctorName`; **Get Text** pentru confirmare.

| Eșec | Cod | Tip |
|---|---|---|
| `in_PatientSource` nu există în listă | `PATIENT_SOURCE_MAPPING_MISSING` | business |
| `in_ReferringDoctorName` ne-gol și absent din lista `Medic trimitator` | `REFERRING_DOCTOR_NOT_FOUND` | business |
| dropdown-ul nu răspunde | `PIXELDATA_UI_TIMEOUT` | system |

`REFERRING_DOCTOR_NOT_FOUND` e cod de business: recepția are un medic pe care PixelData nu îl cunoaște, deci datele trebuie corectate (medicul adăugat în PixelData, sau numele aliniat). Un retry cu aceleași date ar eșua la fel. Dacă clinica decide altceva — câmp lăsat gol, sau medic creat automat — se schimbă aici (gol G9, întrebarea C11).

Comparația numelui: exactă, după `Trim`, fără diferență de majuscule. Dacă lista PixelData scrie medicii cu titlu („DR. ...”) iar recepția nu, potrivirea trebuie relaxată sau numele aliniat în recepție (de verificat pe laptop). `ReferringDoctorParafa` ar putea deosebi doi medici cu același nume, dar nu e folosită în v1.

### 6.9 `SetStatus.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Status` | In | `String` |
| `in_Config` | In | `Dictionary<String,Object>` |

1. **Select Item** pe `Fisa > DropdownStare` cu `in_Status` (din `initialStatus`, implicit `Programat`).
2. **Get Text** pe `DropdownStare` și compară.

| Eșec | Cod | Tip |
|---|---|---|
| valoarea nu există în listă sau nu rămâne selectată | `PIXELDATA_UI_TIMEOUT` | system (de verificat: dacă `initialStatus` greșit e cauza obișnuită, un cod de business ar fi mai corect) |

Câmpul `Stare:` este obligatoriu în PixelData. Fără el, salvarea e blocată de popup-ul `Atenție` / „Nu ați selectat starea pacientului!”. De aceea se setează înainte de salvare și se verifică imediat, nu se descoperă din popup.

### 6.10 `AddProcedures.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Procedure` | In | `String` |
| `in_Config` | In | `Dictionary<String,Object>` |

1. **Type Into** `Fisa > CautareProcedura` cu `in_Procedure` (fereastra sau lista de căutare a serviciilor).
2. Selectarea procedurii din listă și adăugarea ei (`Fisa > ButonAdaugaProcedura`, de verificat mecanismul exact).
3. **Extract Table Data** pe `Fisa > TabelProceduriSelectate` și verifică un rând cu `Denumire` = `in_Procedure` și `Cantitate` = 1.

| Eșec | Cod | Tip |
|---|---|---|
| căutarea nu întoarce procedura | `PROCEDURE_NOT_FOUND` | business |
| lista sau tabelul nu răspund | `PIXELDATA_UI_TIMEOUT` | system |

`in_Procedure` vine din `Mappings.ResolveProcedure(ProductCode, ProductName)`: maparea codului dacă există, altfel `ProductName` ca atare. `PROCEDURE_NOT_FOUND` acoperă ambele cazuri — codul mapat greșit și numele produsului din recepție care nu e denumirea din PixelData. Amândouă se repară în date (`procedures` în `pixeldata-mappings.json`), nu prin retry.

V1 adaugă exact o procedură: o programare din recepție are un singur produs (gol G11). Prețul nu se completează: prețurile pe contract stau în PixelData.

### 6.11 `Save.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Config` | In | `Dictionary<String,Object>` |

1. **Click** `Fisa > ButonSalvare`.
2. **Invoke Workflow File** `PixelData\HandlePopups.xaml` cu `in_Config`, `out_Popup`.
3. **If** `out_Popup` = `StareLipsa` → **Invoke** `PixelData\SetStatus.xaml` din nou, apoi **Click** `ButonSalvare` a doua oară și `HandlePopups.xaml` din nou. Dacă popup-ul reapare: `PIXELDATA_SAVE_UNCONFIRMED`.
4. **Check App State** pe `Fisa > TabPacientSiDocumente`, ramura „nu apare” — fișa se închide după salvare (de verificat: poate rămâne deschisă).

| Eșec | Cod | Tip |
|---|---|---|
| popup „Nu ați selectat starea pacientului!” a doua oară | `PIXELDATA_SAVE_UNCONFIRMED` | system |
| butonul nu răspunde, fișa nu se închide | `PIXELDATA_UI_TIMEOUT` | system |

### 6.12 `HandlePopups.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Config` | In | `Dictionary<String,Object>` |
| `out_Popup` | Out | `String` |

Câte un **Check App State** pentru fiecare popup cunoscut, cu timeout `PopupCheckTimeoutSeconds`. Ferestrele de tip pop-up sunt top-level: dacă un Check App State din interiorul scope-ului fișei nu le vede, se pune în afara lui sau într-un **Use Application/Browser** separat, pe fereastra popup-ului.

| Fereastră | Text | `out_Popup` | Ce face robotul | Cod | Tip |
|---|---|---|---|---|---|
| `Eroare` | „Eroare CNP pacient! CNP incorect. Căutarea returnează eroare” | `CnpRespins` | închide fereastra | `PIXELDATA_CNP_REJECTED` | business |
| `Atenție` | „Nu ați selectat starea pacientului!” | `StareLipsa` | **Click** `ButonInchide` („Închide”), apoi întoarce controlul apelantului | tratat de `Save.xaml` (§6.11) | — |
| `Atenționare` | „Sunteți sigur că doriți eliminarea fișei și revenirea la starea generată?” | `EliminareFisa` | **Click** `ButonNu` | `PIXELDATA_UI_TIMEOUT` | system |
| `Atenție` | „Sunteți sigur că doriți golirea câmpurilor?” | `GolireCampuri` | **Click** `ButonNu` | `PIXELDATA_UI_TIMEOUT` | system |
| altă fereastră modală | — | `Necunoscut` | screenshot (`TakeScreenshot.xaml`), nu apasă nimic | `PIXELDATA_UI_TIMEOUT` | system |
| niciuna | — | `""` | continuă | — | — |

Ultimele trei rânduri sunt ferestre pe care robotul nu are de ce să le provoace: el nu anulează și nu golește nimic. Apariția lor înseamnă că ecranul e într-o stare pe care robotul nu o cunoaște. Răspunsul este mereu `NU` — nimic nu se pierde — urmat de o excepție de sistem: itemul se reia, iar verificarea de idempotență (§6.2) decide dacă mai e ceva de creat. Pe `DA` nu se apasă niciodată.

### 6.13 `VerifySaved.xaml`

| Argument | Direcție | Tip |
|---|---|---|
| `in_Item` | In | `AppointmentItem` |
| `in_Config` | In | `Dictionary<String,Object>` |

1. **Check App State** pe `Programari > GrilaProgramari`, ramura „apare”, timeout `SaveConfirmTimeoutSeconds` (fișa s-a închis, grila zilei e din nou pe ecran).
2. **Extract Table Data** pe `GrilaProgramari`.
3. Verifică rândul orei `in_Item.ScheduledLocalTime`: `Nume` conține numele pacientului și `Serviciu` e ne-gol.

| Eșec | Cod | Tip |
|---|---|---|
| rândul nu apare ocupat în `SaveConfirmTimeoutSeconds` | `PIXELDATA_SAVE_UNCONFIRMED` | system |
| grila nu se încarcă | `PIXELDATA_UI_TIMEOUT` | system |

`Successful` în Orchestrator înseamnă doar că robotul a terminat fără excepție. Confirmarea că programarea chiar există e acest pas. La retry, `CheckExistingAppointment.xaml` o găsește și itemul se încheie cu `already_existed`, fără dublură.

## 7. Object Repository

O bibliotecă UI pentru aplicație: **App `PixelData` > Version (de verificat versiunea reală) > Screens > Elements**. Numele de mai jos sunt cele folosite în §4–§6. Etichetele între ghilimele sunt textele din interfață, așa cum apar în clip; restul sunt de confirmat la captură.

| Screen | Element | Ce este |
|---|---|---|
| `Login` | `FereastraLogin` | fereastra de autentificare |
| | `CampUtilizator`, `CampParola` | user și parolă |
| | `ButonOk` | butonul de intrare |
| | `MesajEroareLogin` | mesajul la parolă greșită (de verificat că există) |
| `Principal` | `FereastraPrincipala` | fereastra aplicației, ancora tuturor celorlalte |
| | `MeniuProgramari` | `PROGRAMĂRI` în panoul vertical din stânga |
| `Programari` | `MiniCalendar` | mini-calendarul din stânga-sus |
| | `AntetLuna` | luna și anul afișate |
| | `ButonLunaAnterioara`, `ButonLunaUrmatoare` | navigarea pe luni |
| | `MiniCalendarZi` | ziua din calendar; selector parametrizat după numărul zilei |
| | `DropdownResursa` | resursa / cabinetul / medicul |
| | `GrilaProgramari` | tabelul zilei: `Ora`, `Nume`, `Observatii`, `Contact`, `Serviciu` |
| | `CelulaOra` | rândul unei ore; selector parametrizat după `HH:MM` |
| `Fisa` | `TabPacientSiDocumente` | sub-tab-ul `Pacient si documente` |
| | `CampNumeCnp` | `Nume/CNP` |
| | `ListaRezultatePacienti` | rezultatele căutării (de verificat forma) |
| | `MeniuDateDemografice` | `Date demografice`, în meniul din stânga fișei |
| | `CheckboxTrimitere` | `Trimitere` |
| | `CampNrSerieBilet` | `Nr./Serie bilet` |
| | `CampDataBilet` | `Data bilet` |
| | `DropdownSursaPacient` | `Sursa pacient:` |
| | `DropdownMedicTrimitator` | `Medic trimitator:` |
| | `DropdownStare` | `Stare:` |
| | `CampContact` | telefonul pacientului (de aflat pe laptop) |
| | `CampObservatii` | `Observatii` |
| | `CautareProcedura`, `ButonAdaugaProcedura` | căutarea și adăugarea serviciului |
| | `TabelProceduriSelectate` | `Proceduri selectate:` — `Cod`, `Denumire`, `Cantitate` |
| | `ButonSalvare` | salvare / confirmare |
| `DateDemografice` | `CampNume`, `CampPrenume`, `CampCnp` | `Nume`, `Prenume`, `CNP` |
| | `ButonSalveazaPacient` | salvarea pacientului nou (de verificat) |
| `Popups` | `PopupEroareCnp` | `Eroare` |
| | `PopupStareLipsa` | `Atenție` |
| | `PopupEliminareFisa` | `Atenționare` |
| | `PopupGolireCampuri` | `Atenție` |
| | `ButonInchide`, `ButonDa`, `ButonNu` | `Închide`, `DA`, `NU` |

Cele două ferestre `Atenție` au același titlu și texte diferite: elementele lor trebuie să conțină textul în selector, altfel se confundă.

## 8. Ordinea construcției

Fiecare workflow se depanează singur, cu Debug File și valori implicite pe argumente, înainte de a fi legat în `Process.xaml` ([docs/10-plan-teste.md](../docs/10-plan-teste.md) §4).

1. `KillAllProcesses.xaml`, `Login.xaml`, `CloseAllApplications.xaml` — fără ele nu se poate captura nimic repetat.
2. `OpenProgramari.xaml`, `SelectDateAndResource.xaml`, `SelectSlot.xaml` — până aici nu se scrie nimic în PixelData.
3. `FindOrCreatePatient.xaml`, `FillPatientAndReferral.xaml`, `SelectPatientSourceAndDoctor.xaml`, `SetStatus.xaml`, `AddProcedures.xaml` — cu pacientul de test.
4. `HandlePopups.xaml`, `Save.xaml`, `VerifySaved.xaml`, `CheckExistingAppointment.xaml`.
5. `Process.xaml`, apoi `Main.xaml` și un item pus în coadă cu `tools/queue-client` ([docs/06-setup-orchestrator.md](../docs/06-setup-orchestrator.md) §9).

Pașii 2–4 creează programări reale. Doar cu pacientul de test și resursa de test, iar la final se șterg ([docs/10-plan-teste.md](../docs/10-plan-teste.md) §8).
