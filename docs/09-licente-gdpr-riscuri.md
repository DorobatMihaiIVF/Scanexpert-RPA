# 09 — Licențe, GDPR, riscuri

Ce permite licența UiPath, unde ajung datele pacienților, ce trebuie clarificat cu PixelData SRL, ce poate merge prost și alternativa fără robot. Întrebările deschise au ID-uri din [11](11-intrebari-deschise.md).

## 1. Licențe UiPath

| Plan | Include | Producție | Sursă |
|---|---|---|---|
| Community | 1 Unattended, 1 Plus, 1 Pro, Integration Service, Computer Vision 30 MP/min | nu: Community Agreement V.2022.11.07, §3.1 și §4.2, permite doar scop necomercial; folosirea în producție la o clinică probabil îl încalcă | investigatie §4 „Licențiere” |
| Standard Trial (contul actual) | tenant: 2 Unattended, 2 Testing; 2 utilizatori Pro | nu: non-producție; expiră la 2026-11-09, apoi downgrade la Free | investigatie §3, §4 |
| Basic (plătit) | ≤ 2 Unattended | da | investigatie §4 |
| Alt plan plătit | după ofertă | da | de verificat |

| Consecință | Detaliu |
|---|---|
| Fazele 1–2 | pe trial, doar cu date de test |
| Prima programare reală | abia pe plan plătit; Basic pare suficient pentru un laptop (de verificat, U4) |
| Expirarea trial-ului | efectul asupra cozii, procesului și triggerului: de verificat (U4) |

**Studio Web arată „Unlicensed”**

| Cauză probabilă | Rezolvare, oricare |
|---|---|
| utilizatorul nu e în grupul Automation Developers, căruia îi e alocată una din cele 2 licențe Pro (neconfirmat, investigatie §3) | 1) adaugă utilizatorul în grupul Automation Developers; 2) alocă-i direct licența Pro liberă. Căile exacte din meniul Admin: de verificat |

Studio Web nu automatizează aplicații desktop (investigatie §4), deci pentru PixelData e nevoie de Studio pe laptop. Rezolvarea contează doar pentru lucrul în Studio Web. Configurarea contului: [06](06-setup-orchestrator.md).

## 2. GDPR

Pentru v1 utilizatorul a ales ca itemul din coadă să conțină toate datele programării (investigatie §1). Datele sunt date personale și date privind sănătatea.

| Unde | Ce date | Risc | Măsură |
|---|---|---|---|
| Coada UiPath Cloud (`SpecificContent`) | nume, CNP, telefon, data nașterii, sex, investigație, medic trimițător, observații | date de sănătate la un terț, persoană împuternicită | DPA cu UiPath (U3); rezidența datelor (de verificat, U1); retenția itemilor (de verificat, U2, C15); acces în Orchestrator doar pentru conturile care au nevoie |
| `Output`, `ProcessingException`, logurile joburilor în Orchestrator | ce scrie robotul | date de pacient în loguri | robotul scrie doar coduri, id-uri și statusuri; niciun Log Message cu valori din item |
| Laptop: `Exceptions_Screenshots` (REFramework), loguri locale | capturi cu fișa pacientului | pierderea sau furtul laptopului, acces neautorizat | BitLocker; cont Windows dedicat, cu parolă; acces fizic restrâns; curățarea periodică a capturilor (de decis); `.gitignore` exclude `Exceptions_Screenshots/` |
| Repo: `contracts/examples`, teste, `tools/queue-client` | date de test | date reale copiate în repo | doar date fictive |
| Recepție, tabel outbox (faza 3) | id-uri și statusuri | — | `last_error` și callback-ul fără date de pacient ([05](05-integrare-receptie.md)) |

**Minimizare: alternativa respinsă pentru v1**

| Opțiune | Pro | Contra |
|---|---|---|
| Date complete în item (ales) | robotul nu depinde de recepție la rulare; contract simplu; testabil cu `queue-client` | date de sănătate în UiPath Cloud: DPA, rezidență, retenție |
| Doar `AppointmentId` în item | nicio dată de pacient în coadă | robotul citește datele din recepție: endpoint de mașină, rețea de la laptop, token; recepția devine necesară la fiecare rulare; contract v2 |

Temeiul legal și evaluarea de impact (DPIA) le stabilește clinica, ca operator de date (de verificat cu responsabilul GDPR al clinicii).

## 3. Condiții PixelData

Producător: SC PixelData SRL, Cluj-Napoca (contact în investigatie §4 „PixelData”).

| Subiect | De ce contează | Stare |
|---|---|---|
| User PixelData dedicat robotului | se vede ce a creat robotul; parolă separată, în assetul `PixelData_RobotLogin` | de creat de clinică |
| Logări concurente ale aceluiași user | robotul și un om, sau două sesiuni | de întrebat (P3) |
| Automatizarea interfeței permisă de licență | folosire în producție | de întrebat (P3) |
| Anunțarea actualizărilor, mediu de test | riscul R4 | de întrebat (P5) |

## 4. Riscuri

| # | Risc | Impact | Probabilitate | Atenuare | Responsabil |
|---|---|---|---|---|---|
| R1 | Programare salvată pe jumătate: jobul cade între completare și confirmare | dublură sau fișă incompletă în PixelData | medie | verificare „există deja?” (CNP + dată + oră + resursă) înainte de creare ⇒ `already_existed`; `PIXELDATA_SAVE_UNCONFIRMED` reîncearcă prin aceeași verificare; itemii Failed verificați de un om | Dezvoltator RPA |
| R2 | Laptop oprit, în sleep sau fără rețea | programările întârzie; itemii rămân New, joburile Pending; robot fără heartbeat 2 min = deconectat | mare | pe alimentare Sleep = Never, capac = Do nothing, ore active pentru Windows Update; itemii nu expiră; `APPOINTMENT_IN_PAST` oprește programările trecute; verificarea zilnică a cozii (C14) | Clinică |
| R3 | Rezoluție sau scalare DPI schimbate (monitor extern) | click în locul greșit în grila de sloturi; selectori, CV sau imagini eșuează | medie | aceeași rezoluție și scalare (ideal 100%) la dezvoltare și la rulare; Resolution Width/Height/Depth în setările robotului; fără monitor extern la rulare; verificare cu Get Text după selecție | Dezvoltator RPA |
| R4 | Interfața PixelData se schimbă după o actualizare | selectori rupți ⇒ `PIXELDATA_UI_TIMEOUT` pe toți itemii | medie | UI Library pe versiuni de aplicație (App > Version > Screens > Elements); actualizări anunțate (P5); test de fum după actualizare ([10](10-plan-teste.md)); trigger oprit în timpul actualizării | Dezvoltator RPA + Clinică |
| R5 | Windows Home: doar sesiune de consolă | robotul nu are sesiune RDP proprie; jobul ocupă consola; laptopul nu se poate folosi în paralel | de verificat (C8) | Windows 10/11 Pro, Enterprise sau Education cu Login To Console = No; pe Home: Login To Console = Yes și omul se deloghează (nu Win+L) înainte de orele robotului | Clinică + Administrator UiPath |
| R6 | Parola Windows a contului `robot-pixeldata` expiră sau e schimbată | robotul nu deschide sesiunea ⇒ joburi eșuate | medie (politica locală de verificat) | parolă fără expirare pentru contul local dedicat (de verificat) sau schimbare planificată; la schimbare se actualizează și în Orchestrator | Administrator UiPath |
| R7 | Trial-ul Standard expiră la 2026-11-09 | downgrade la Free; efect asupra Unattended, coadă, trigger (U4) | sigur | decizia de plan plătit înainte de expirare; pe trial doar date de test | Proprietarul proiectului |
| R8 | Un om folosește laptopul în orele robotului sau îl blochează cu Win+L | „Cannot bring the target application in foreground because the Windows session is locked”; job întrerupt | medie | ore fixe pentru robot (C9); delogare, nu blocare; setările GPO din investigatie §4 „Laptopul Windows” | Clinică |
| R9 | Userul PixelData al robotului: parolă expirată sau login concurent refuzat | `PIXELDATA_LOGIN_FAILED` pe toți itemii | de verificat (P3) | user dedicat; parolă în `PixelData_RobotLogin`; întrebare la PixelData SRL | Clinică |
| R10 | Programare anulată în recepție după ce a fost creată în PixelData, inclusiv auto-anularea BR-08 | programare fantomă în PixelData până la `cancel` (v2) | mare pentru CAS/Monitor fără bilet | decizia 3 din [05](05-integrare-receptie.md) §11; anulare manuală până atunci | Echipa ScanExpert + Clinică |
| R11 | Mapări rămase `"TODO"` | itemi Failed Business (`RESOURCE_MAPPING_MISSING`, `PATIENT_SOURCE_MAPPING_MISSING`) | mare la început | completare pe laptop înainte de producție ([04](04-mapare-campuri.md)) | Dezvoltator RPA + Clinică |

## 5. Alternativa: HL7 inbound în PixelData

Pagina PixelData RIS spune „sistem deschis HL7 și DICOM” (MWL, MPPS, programare pe unități/camere, interfață CNAS), dar nu există o specificație publică API/HL7 și nu e clar dacă acceptă programări inbound (investigatie §4 „PixelData”). Dacă acceptă SIU^S12 (programare) și ADT^A04/A08 (pacient), sau ORM^O01, recepția poate trimite mesaje direct, fără robot.

| Criteriu | RPA (v1) | HL7 inbound |
|---|---|---|
| Depinde de producător | nu | da: interfață nedocumentată public (P1) |
| Fragilitate | mare: interfață, rezoluție, actualizări | mică: mesaje standard |
| Infrastructură | laptop Windows pornit, UiPath Cloud | conexiune de rețea spre PixelData (de verificat) |
| Date de pacient la terți | da, în UiPath Cloud | nu |
| Cost | plan UiPath plătit | licența interfeței HL7 (de verificat, P1) |

Recomandare: întrebarea P1 pleacă acum; RPA continuă. Dacă PixelData acceptă programări inbound, HL7 devine faza 4, iar robotul rămâne rezervă.
