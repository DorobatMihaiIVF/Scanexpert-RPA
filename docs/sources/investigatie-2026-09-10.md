# Note investigație — 2026-09-10

Note brute din investigația inițială: codul recepției (mono-ymirr), documentația UiPath, starea contului UiPath. Sursă pentru documentele din `docs/`. Nu conține secrete. `[U]` = neconfirmat, `[3P]` = sursă terță.

## 1. Deciziile utilizatorului
- Proiect separat în `/home/mihai/Documents/pixeldata-programari-rpa`, repo git propriu. Nicio modificare în mono-ymirr în această fază.
- Declanșare dorită: când se creează o programare în recepția ScanExpert, ea ajunge automat la robot (fără Excel manual).
- Itemul din coadă conține TOATE datele programării, nu doar ID-ul. Implicațiile GDPR se documentează.
- v1: doar programări noi. Anulare, mutare și auto-anulare vin în faze ulterioare.
- Robotul creează și pacienți noi în PixelData („Date demografice”).
- PixelData cere login cu user și parolă.
- UiPath: acum există doar contul Automation Cloud. Studio și Robot se instalează mai târziu pe un laptop Windows care are PixelData. Dezvoltarea se face acum pe Linux.
- Fără .NET SDK pe Linux: testele C# se scriu acum și rulează pe laptop.
- Utilizatorul este începător în RPA.

## 2. Recepția ScanExpert (mono-ymirr), din cod
Prefixe: R = `mono-ymirr/r3/apps/scanexpert/reception`, B = `mono-ymirr/r3/apps/scanexpert/backend`, M = `B/db/migrations`.

### Stack
- Go 1.26, stdlib `net/http`, JS vanilla. Postgres partajat `scanexpress`, SQL scris de mână cu pgx.
- Recepția nu deține schemă. Toate migrările sunt în M (goose, 00001..00090).

### Creare programare: `POST /api/appointments`
Rută R/routes.go:87, handler R/customer_appointments.go:34.

Câmpuri body:
- `customer_id`* (UUID, pacientul trebuie să existe)
- `kind`* (text, numele investigației)
- `scheduled_at`* (RFC3339, sau `2006-01-02T15:04[:05]` / `2006-01-02 15:04` interpretate ca Europe/Bucharest; UI trimite `YYYY-MM-DDTHH:MM` fără zonă)
- `notes` (≤ 2000)
- `payer` (`''`, `CAS`, `Monitor`, `Contra cost`, `Asigurator privat`)
- `branch_id`, `product_id`
- `referral_pending` (bool)
- `laterality` (`''`, `stanga`, `dreapta`, `bilateral`)
- `referring_doctor_id`
- `referral_date` (`YYYY-MM-DD`)
- `insurer` (obligatoriu doar la `Asigurator privat`)
- `overbook` (bool)

Setate de server: `created_by` = emailul operatorului, `source` = `"Centrala"`.

Răspunsuri:
- 201 `{"id"}`
- 422 `{"errors":{camp: mesaj}}`
- 409 `{error, can_force:true}` (ora e plină)
- 404 pacient lipsă; 503; 400

Pacientul se creează înainte, prin `POST /api/customers` (R/customers.go:170):
- câmpuri: `full_name`, `phone` (obligatoriu, E.164), `cnp`, `email`, `birth_date`, `note`, `force`
- 409 la nume + telefon duplicat; 422 la CNP duplicat

### Formularul „Programare nouă” (R/web/js/booking-form-fields.js:193-250)
- Nume*, Prenume*, CNP (13 cifre + checksum; obligatoriu la sediu), Telefon*
- Sucursală*, Serviciu* (modalitate), Investigație* (produs din catalog)
- Tip recomandare*: CAS / Bilet Monitor / Asigurător privat (+ nume asigurător) / Cu plată (`Contra cost`)
- Doar pentru CAS/Monitor: document bilet, data biletului, medic trimițător (nume, clinică, email, telefon)
- Lateralitate, după regula produsului
- Zi + oră din sloturi
- Observații

### Salvarea în browser (R/web/js/booking-submit.js:130-234)
- Ordinea pașilor: medic → pacient → programare → atașare bilet → WhatsApp → drafts.
- Salvarea NU este atomică: dacă un pas de după programare eșuează, programarea rămâne salvată.

### Efecte la creare
- Doar o linie de log.
- Niciun eveniment, notificare, job sau apel HTTP extern.

### Cine scrie în `appointments` (4 locuri)
1. recepția (`source` = Centrala)
2. panoul backoffice, `POST /appointments` (B/internal/features/appointments/admin_write.go:244-255)
3. API-ul aplicației pacient, `POST /api/v1/appointments` (B/internal/features/appointments/create_api.go:285-299, `source` = `''`)
4. intents DraftAppointment (B/cmd/intents/clinic_writes.go:216-240, oprit pe dev)

### Tabele
- `customers`: `full_name` (UN singur câmp), `cnp` (opțional; unic când nu e gol), `email`, `birth_date`, `sex`, `address`, `insurance_*`. Telefonul e în `customer_phones` (`phone_e164`, `is_primary`).
- `appointments`:
  - identificare și stare: `id`, `customer_id`, `kind`, `scheduled_at` (timestamptz), `status` (`programat` | `confirmat` | `necesita_contact` | `anulat`), `confirmed_at`, `cancelled_at`, `created_by`, `notes`
  - programare: `payer`, `branch_id`, `product_id`, `overbooked`, `laterality`, `insurer`
  - scris doar de alții: `equipment_id` (recepția nu îl scrie), `practitioner_id` (doar API-ul pacient)
  - bilet: `referral_due_at`, `referring_doctor_id`, `referral_date`
  - `source` (`''` / Centrala / Aplicație / La sediu)
  - anulare: `cancel_reason`, `cancel_origin`, `cancelled_by`
- `doctors`: `full_name`, `clinic`, `phone`, `email`, `cuim`, `parafa`.
- `branches`, 10 sedii: ScanExpert Iași, Iași II, Galați, Brașov, Roman, Pașcani; OptimDiagnostic Botoșani, Botoșani II; ScanExpert Timișoara, Timișoara II.
- `products`: `barcode` (cod catalog, UNIQUE), `name`, `modality`, `duration_minutes`, `laterality_rule`. Prețuri per sediu, nativ/CIV (`product_prices`).
- Nu există tabel cu mai multe servicii pe programare: 1 produs / programare.

### Corespondență PixelData ↔ recepție
- Nume/Prenume: PARȚIAL. Un singur `full_name`; UI-ul îl împarte euristic, primul cuvânt = Nume (R/web/js/booking-name.js).
- CNP: există, dar e opțional (poate fi gol). Checksum în R/helpers.go:119-141.
- Telefon: există (E.164; pot fi mai multe).
- Pacient nou vs existent: nu există flag. Robotul caută în PixelData după CNP/nume.
- Dată + oră: `scheduled_at`. Durata = `products.duration_minutes`.
- Resursă PixelData (ex. „C.A.L.A.T.I.”): LIPSEȘTE. Cel mai apropiat: sucursala (ex. „ScanExpert Galați”) + modalitatea. E nevoie de un tabel de corespondență. Presupunerea „C.A.L.A.T.I.” = Galați nu e verificată.
- Trimitere (checkbox): DERIVAT, din `payer` ∈ {CAS, Monitor} și/sau `referral_date`.
- Nr./Serie bilet: LIPSEȘTE (există doar `waiting_list.referral_series`).
- Data bilet: `referral_date`.
- Sursa pacient: AMBIGUU. Poate fi clinica medicului trimițător (`doctors.clinic`) sau canalul programării (`appointments.source`).
- Medic trimițător: `referring_doctor_id` → `doctors.full_name` (opțional).
- Stare: recepția are `programat`/`confirmat`/`necesita_contact`/`anulat`; PixelData are „În lucru”, „Programat” etc. E nevoie de corespondență.
- Proceduri (Cod, Denumire, Cantitate, Preț): PARȚIAL. Un produs, cantitate 1. Cod = `products.barcode` (recepția nu îl expune), Denumire = `products.name` / `kind`. Prețurile pe contract sunt în PixelData (B/norn/plans/clinic_operations.py:319).
- Observații: `notes`.

### Ciclul de viață
- Confirmare: `POST /api/appointments/{id}/confirm`, cel mult cu 1 zi înainte.
- Modificare: `PATCH /api/appointments/{id}`. Mutarea orei readuce statusul la `programat`.
- Anulare: `POST /api/appointments/{id}/cancel` cu `{reason, origin pacient|clinica}`.
- Auto-anulare BR-08: CAS/Monitor fără bilet în 2h → `anulat`, `cancel_origin=sistem`. Rulează orar (R/referrals.go:149-182).

### Puncte de integrare (faza 2)
- H1, handler-ul recepției: ratează ceilalți 3 scriitori.
- H3, trigger DB + tabel outbox prin migrare backend: acoperă toți 4. Recomandat.
- H4, CDC: `wal_level=logical` e deja activ.
- Model de copiat: coada WhatsApp durabilă, `queued` → `sending` → `sent|failed`, cu reluare la pornire (R/whatsapp_send_queue.go:9-32, :140-185). Coloane de retry: M/00086_whatsapp_media_attempts.sql.
- Autentificare de mașină (Bearer) pentru callback: R/machine.go:59-82.
- Variabilele de mediu se declară în R/env_registry.go. Secretele stau în setul hel `scanexpert-monorepo-reception-dev`.
- Recepția rulează cu o singură replică, deci coada nu poate sta în memorie.

### Deploy dev
- k8s, namespace `scanexpert-dev`, prin niu/Pulumi.
- Podul face deja apeluri HTTPS externe (Meta, ElevenLabs), deci UiPath Cloud e probabil accesibil [U, neverificat pe cluster].

### PixelData și UiPath în repo
- PixelData apare doar în planuri (B/norn/plans/clinic_operations.py:106, :319, :540-546). Acolo e o întrebare deschisă: „înlocuim / extindem / stăm lângă PixelData?”.
- UiPath/RPA: zero mențiuni.

## 3. Contul UiPath Automation Cloud (stare la 2026-09-10)
- Org „IVF”, un tenant `DefaultTenant`. Plan Standard Trial, expiră la 2026-11-09 (non-producție).
- Licențe:
  - tenant: 2 Unattended, 2 Testing
  - utilizatori Pro: 2, dintre care 1 alocat grupului Automation Developers
  - Studio Web arată „Unlicensed” (probabil utilizatorul nu e în grupul Automation Developers [U])
- Servicii: Orchestrator, Actions, Maestro, Integration Service, Data Fabric, Insights, Test Manager.
- Orchestrator gol:
  - foldere: `Shared` + `My Workspace`; 0 procese, cozi, assets, triggere, pachete
  - mașini: Default Serverless + template personal; niciun machine template on-prem
  - `Default Robot`: robot account, Unattended, fără Domain\Username
  - meniul de triggere are Time / Queue / Event / API Triggers; Webhooks există în meniul tenantului
- External Applications: 0.
- Studio/Robot 2025.10.17 se descarcă din Resource Center (nimic descărcat).

## 4. Documentația UiPath: concluzii

### Declanșare din backend
Recomandat: Orchestrator API AddQueueItem + Queue Trigger.

1. Token:
   - `POST https://cloud.uipath.com/{org}/identity_/connect/token`
   - form: `grant_type=client_credentials&client_id=..&client_secret=..&scope=OR.Queues`
   - `expires_in` 3600, fără refresh token: token-ul se păstrează în cache și se cere din nou la expirare.
2. External Application: Admin > External Applications > Confidential > Application scopes. Se adaugă în folder cu un rol care are Queues View + Transactions Create/View [U, rolul minim].
3. Adăugare item:
   - `POST https://cloud.uipath.com/{org}/{tenant}/orchestrator_/odata/Queues/UiPathODataSvc.AddQueueItem`
   - headere: `Authorization: Bearer <token>`, `Content-Type: application/json`, `X-UIPATH-OrganizationUnitId: {folderId}` (sau `X-UIPATH-FolderPath: <cale folder>`)
   - body: `{"itemData":{"Name":"<queue>","Priority":"Normal","Reference":"<ref>","SpecificContent":{...}}}`; opțional `DeferDate`, `DueDate`
   - răspuns: 201 + itemul (Status New)
4. Idempotență:
   - setarea cozii „Enforce unique references” respinge un Reference duplicat („Duplicate Reference”; codul HTTP exact [U]); se tratează ca „deja în coadă”
   - Reference: maximum 128 caractere, fără apostrof [3P]
   - SpecificContent: maximum ~256.000 caractere / 512.000 bytes
5. Retry în coadă:
   - Auto retry, maximum 1–50 încercări
   - se reîncearcă doar Application exceptions, nu Business exceptions
   - un item In Progress fără actualizare 24h devine Abandoned
6. Queue trigger:
   - pornește la adăugarea unui item și reverifică la 30 min (minimum 10)
   - setări: minimum items, „Maximum pending and running jobs” (recomandat 1), un job nou per N items
   - necesită runtime Unattended
7. Status:
   - `GET {base}/odata/QueueItems?$filter=Reference eq '<ref>'&$orderby=Id desc` (scope OR.Queues)
   - câmpuri: Status (New / InProgress / Successful / Failed / Abandoned / Retried), Output, ProcessingException
   - retry-urile au același Reference, deci contează cel mai mare Id

Alternative fără dedup și fără retry: API Triggers (`{base}/t/{folderKey}/{slug}`), Integration Service HTTP Webhook, StartJobs. Nerecomandate.

Raportarea rezultatului înapoi:
- Robotul apelează API-ul recepției cu HTTP Request (UiPath.WebAPI.Activities, retry exponențial) din SetTransactionStatus.
- Recepția verifică periodic QueueItems după Reference (reconciliere).
- Webhooks Orchestrator (queueItem.transactionCompleted etc., semnătură `X-UiPath-Signature` HMAC-SHA256): pierd evenimente, deci doar opțional.

Surse:
- https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/transactions-requests
- https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/building-api-requests
- https://docs.uipath.com/automation-cloud/automation-cloud/latest/api-guide/accessing-uipath-resources-using-external-applications
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/managing-queues-in-orchestrator
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/queue-triggers
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/api-triggers
- https://docs.uipath.com/activities/other/latest/developer/http-request
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-webhooks

### Licențiere
- Community (Unified Pricing, aug 2026) include 1 Unattended, 1 Plus, 1 Pro, Integration Service și Computer Vision 30 MP/min.
- Community Agreement V.2022.11.07, §3.1 și §4.2: licența e DOAR pentru scop necomercial. Folosirea în producție la o clinică probabil îl încalcă.
- Trial Standard și Basic: non-producție, 60 de zile, apoi downgrade la Free.
- Producție = plan plătit (Basic permite ≤ 2 Unattended).

Surse:
- https://docs.uipath.com/automation-cloud/automation-cloud/latest/admin-guide/unified-pricing-licensing-plan-framework
- https://assets.ctfassets.net/5965pury2lcm/7jgRlEw7oQ47yPj3i7qEQ6/2ad5e305fb70ef436aba10cb15d76e3d/UiPath_Community_Agreement.pdf

### Proiect și scrierea codului
- Template: REFramework, inclus în Studio („Robotic Enterprise Framework”):
  - stări Init / Get Transaction Data / Process / End; input din coadă; retry
  - screenshot la excepție (în `Exceptions_Screenshots`)
  - Config.xlsx cu foile Settings / Constants / Assets
- Compatibilitate Windows (.NET 8); Cross-platform NU suportă aplicații desktop. Limbajul expresiilor: C#.
- Studio 2025.10 LTS. Pachete (2026-09): UiPath.System.Activities 26.8.1, UiPath.UIAutomation.Activities 25.10.40 (LTS) / 26.10.3, UiPath.Testing.Activities 25.10.3. Cu Studio 2025.10, versiunile le alege Studio.
- `github.com/UiPath/ReFrameWork` e ARHIVAT din 2026-06-26, deci nu se folosește. Proiectul se generează din Studio.
- `project.json` și XAML scrise de mână: oficial nerecomandat („troubleshooting only”). Scheletul se creează în Studio, pe laptop.
- Coded workflows (C#), permise în proiectele Windows:
  - coded workflow: clasă `: CodedWorkflow` cu `[Workflow] Execute`
  - coded test case
  - code source file: clase C# simple
  - legătura XAML ↔ coded se face prin Invoke Workflow File
- Pe Linux:
  - logica C# pură (fără `using UiPath`) stă în fișiere .cs care se copiază apoi în proiect
  - proiectul de teste e separat și stă ÎN AFARA folderului proiectului UiPath, fiindcă Studio compilează toate .cs din folderul proiectului
  - testele includ logica prin `<Compile Include="../../robot/Logic/**/*.cs" />`, țintă `net8.0`, xunit, rulare cu `dotnet test`
- Nu se poate pe Linux:
  - build / pack / analyze pentru un proiect Windows (`uip rpa build` cere un runner Windows)
  - automatizarea aplicațiilor desktop din Studio Web
- Pe laptop, în Studio:
  1. New REFramework (Windows, C#)
  2. copiere `Logic/*.cs` în proiect
  3. captură UI PixelData în Object Repository (UI Library per aplicație: App > Version > Screens > Elements)
  4. XAML per ecran → debug → publish
- Agentul de codare oficial UiPath: CLI `uip` (`npm install -g @uipath/cli`) + github.com/UiPath/skills (skill `uipath-rpa`). Comenzile `uip rpa` controlează Studio local prin IPC, deci merg doar pe Windows cu Studio instalat. Există probleme de compatibilitate între versiuni (issues #241, #261) [U].

Surse:
- https://docs.uipath.com/studio/standalone/latest/user-guide/robotic-enterprise-framework
- https://docs.uipath.com/activities/other/latest/ui-automation/ui-automation-project-compatibility
- https://docs.uipath.com/studio/standalone/latest/user-guide/about-the-projectjson-file
- https://docs.uipath.com/studio/standalone/latest/user-guide/coded-workflow
- https://docs.uipath.com/uipath-cli/standalone/latest/user-guide/uip-rpa-build
- https://docs.uipath.com/studio-web/automation-cloud/latest/user-guide/using-ui-automation
- https://docs.uipath.com/studio/docs/reusing-objects-ui-libraries
- https://docs.uipath.com/coding-agents/standalone/latest/user-guide/build-rpa-workflows
- https://github.com/uipath/skills

### Automatizarea aplicației desktop (PixelData, probabil Delphi [U])
- Use Application/Browser: File path = exe PixelData; Open „If not open”; Close „Always” la final. Activități moderne: Click, Type Into, Select Item, Get Text, Extract Table Data.
- Țintire:
  - ordinea: selector Strict/Fuzzy (principal) → Computer Vision (secundar) → Image (ultimul)
  - ancore recomandate
  - framework UI Explorer: Default / Active Accessibility / UI Automation
  - metodă de input: Simulate / SendWindowMessages / HardwareEvents; controalele Delphi pot cere HardwareEvents [U]
- Grila de sloturi orare, în ordinea preferinței:
  1. Extract Table Data, dacă celulele sunt expuse
  2. CV sau text + ancoră + Click Double
  3. tastatură (săgeți + Enter)

  Rezoluția și DPI trebuie să fie aceleași la dev și la robot.
- Dropdown: Select Item; altfel Click + Type Into + Enter + verificare cu Get Text.
- Popups:
  - Check App State (appear/disappear, timeout implicit 5 s) după salvare
  - popup-urile top-level pot cere Check App State în afara scope-ului sau un Use Application separat
  - erorile de date → BusinessRuleException (fără retry); erorile de UI → ApplicationException (retry prin coadă)

Surse:
- https://docs.uipath.com/activities/other/latest/ui-automation/n-application-card
- https://docs.uipath.com/activities/other/latest/ui-automation/n-check-state
- https://docs.uipath.com/activities/other/latest/ui-automation/troubleshooting-selectors

### PixelData (producătorul)
- SC PixelData SRL, Cluj-Napoca (infopxd@pixeldata.ro, 0738 827 300). Produse: PixelData RIS, PACS, Viewer, Cloud.
- Pagina RIS spune „sistem deschis HL7 și DICOM”: MWL, MPPS, programare pe unități/camere, interfață CNAS.
- Nu există specificație publică API/HL7. Nu e clar dacă acceptă programări INBOUND.
- De întrebat producătorul:
  - HL7 v2 ADT^A04/A08 + SIU^S12 (sau ORM^O01)
  - import prin bază de date sau web service
  - logări concurente pentru un user dedicat robotului

Surse:
- https://pixeldata.ro/
- https://pixeldata.ro/pixeldata-ris/

### Laptopul Windows (robot)
**Ediție și conturi**
- Host RDP doar pe Win 10/11 Pro/Enterprise/Education. Pe Home merge doar Login To Console = Yes.
- Cont Windows LOCAL separat pentru robot, cu parolă (nu PIN, nu cont Microsoft [U]).
- User PixelData dedicat robotului.

**Putere, ecran, notificări**
- Pe alimentare: Sleep = Never; capac închis = Do nothing; ore active pentru Windows Update.
- Aceeași rezoluție și scalare (ideal 100%) la dev și la robot. Un monitor extern le schimbă pe amândouă.

**GPO pentru erori cunoscute**
- Eroarea „Cannot bring the target application in foreground because the Windows session is locked”: dezactivează „Enable news and interests on the taskbar”, „Set time limit for active but idle Remote Desktop Services sessions” și „Sign-in and lock last interactive user automatically after a restart”.
- Eroarea „A specified logon session does not exist”: dezactivează „Display information about previous logons during user logon”, apoi `gpupdate /force`.

**Instalare și conectare**
- Instalare: `UiPathStudioCommunity.msi` (sau Enterprise pentru trial) din Resource Center → Custom → „Install for all users” → Studio + „Unattended Robot” (service mode, drepturi de admin). Quick install = user mode = doar attended.
- Orchestrator: Machines > machine template (sau standard machine) > 1 runtime Unattended > Client ID/Secret (secretul se afișează o singură dată).
- Robot account „foreground and background jobs” + Domain\Username + parola Windows.
- Folderul conține: robot account + mașină + proces + coadă + trigger.
- Conectare: `UiRobot.exe connect --url https://cloud.uipath.com/{org}/{tenant}/orchestrator_ --clientID <id> --clientSecret <secret>` (verifică `UiRobot.exe --help`; în PowerShell, escape pentru `$`).
- Setări robot: Login To Console = No (pe Pro, sesiune RDP proprie) + Resolution Width/Height/Depth ca la dev. Pe Home: Yes.

**Reguli de folosire**
- Windows desktop suportă o singură sesiune RDP și o singură sesiune consolă activă. Un job de consolă închide sesiunea RDP activă.
- Omul se DELOGHEAZĂ (nu Win+L) înainte de orele robotului.
- Nu depana în Studio cât rulează un job unattended.

**Laptop oprit sau offline**
- Itemii stau New în coadă, fără expirare. Deadline-ul schimbă doar prioritatea, deci regula „data programării a trecut” se verifică în cod.
- Joburile rămân Pending până revine robotul. Triggerul reverifică la 30 min.
- Un robot fără heartbeat 2 min e considerat deconectat.

**Cădere în mijlocul unui job**
- Robotul repornește jobul la reconectare, dar itemul In Progress nu e reluat automat. După 24h devine Abandoned; se reîncearcă doar dacă e activ auto-retry.
- RISC: programare pe jumătate în PixelData. Soluție: un pas idempotent „există deja programarea? (CNP + dată + oră + resursă)” înainte de creare.

**PixelData deja deschis**
- Use Application se atașează doar la ferestrele din aceeași sesiune.
- În Init: închide instanța veche din sesiunea robotului, deschide una nouă, login cu Credential asset.
- Evită Kill Process după nume: poate închide PixelData din alte sesiuni.

**Diverse**
- Activează Focus assist / Do Not Disturb pentru contul robotului.
- Dacă se schimbă parola Windows a robotului, actualizeaz-o și în Orchestrator.

Surse:
- https://docs.uipath.com/robot/standalone/latest/admin-guide/windows-sessions
- https://docs.uipath.com/robot/standalone/latest/admin-guide/session-troubleshooting
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/unattended-robot-setup
- https://docs.uipath.com/robot/standalone/latest/admin-guide/installing-with-uipath-studio-msi-unattended
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/creating-a-queue-trigger
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-jobs

### CNP (de verificat cu specificația oficială)
- 13 cifre: S AA LL JJ ZZ NNN C.
- Control: ponderile `279146358279`, rest = suma(d[i]*w[i]) % 11; rest 10 → C = 1, altfel C = rest.
- S (secol și sex):
  - 1/2: 1900–1999
  - 3/4: 1800–1899
  - 5/6: 2000–2099
  - 7/8: rezidenți străini
  - 9: străini
- LL 01–12; data trebuie să fie validă.
- JJ: 01–46, 51, 52 (41–46 = sectoarele București; 47/48 istorice) [U].
- Implementarea din recepție: R/helpers.go:119-141 (`validCNP`).
