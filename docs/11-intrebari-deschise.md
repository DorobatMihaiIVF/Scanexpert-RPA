# 11 — Întrebări deschise

Pe destinatari. Coloana „Blochează” spune ce nu poate avansa fără răspuns. Răspunsul se scrie în tabel, cu data, iar documentul afectat se corectează.

## Clinică

| ID | Întrebare | De ce contează | Blochează | Răspuns |
|---|---|---|---|---|
| C1 | Ce resursă PixelData (cabinet, aparat, medic) corespunde fiecărei sucursale și modalități? Lista completă a resurselor din drop-down. | `resources` din mapări; fără ea `RESOURCE_MAPPING_MISSING` | faza 2, test end-to-end | |
| C2 | Ce înseamnă „Sursa pacient”: clinica medicului trimițător, canalul programării sau altceva? Care e lista valorilor? | `patientSource.strategy`; fără ea `PATIENT_SOURCE_MAPPING_MISSING` | faza 2, test end-to-end | |
| C3 | Ce „Stare” primește o programare nouă făcută de recepție („Programat”, „În lucru”)? Ce stări corespund lui `confirmat`, `necesita_contact`, `anulat`? | `initialStatus`; fără stare PixelData refuză salvarea | faza 2; `update`/`cancel` în v2 | |
| C4 | „Cod” din „Proceduri selectate” este același cu codul de catalog din recepție (`products.barcode`)? | `procedures` din mapări | faza 2 | |
| C5 | De unde vin seria și numărul biletului de trimitere (tastate de operator, citite de pe bilet)? Sunt obligatorii în PixelData? | `ReferralNumber` e `""` în v1 | producție pentru CAS/Monitor; schema în faza 3 | |
| C6 | CNP-ul este obligatoriu mereu în PixelData? Care e fluxul pentru pacienți fără CNP (străini, minori, pacient care nu îl dă)? | robotul refuză implicit fără CNP (`CNP_REQUIRED`) | producție | |
| C7 | O programare poate avea mai multe proceduri (ex. consultație + investigație, ca în clip)? Se adaugă ceva automat? | recepția are 1 produs pe programare; v1 adaugă o singură procedură | producție | |
| C8 | Ce ediție de Windows are laptopul (Home, Pro, Enterprise, Education)? | pe Home robotul rulează doar în consolă (R5) | faza 2, setup laptop | |
| C9 | În ce ore poate folosi robotul laptopul și când îl folosește un om? | program, Login To Console, reguli de delogare (R8) | faza 2, setup laptop | |
| C10 | Programările CAS/Monitor fără bilet (anulate automat după 2h, BR-08) intră în PixelData imediat sau abia după ce vine biletul? | programări fantomă în PixelData (R10) | faza 3 | |
| C11 | Ce face robotul când medicul trimițător nu există în lista PixelData: lasă gol, adaugă medicul sau oprește itemul? | tratarea câmpului „Medic trimițător” | faza 2 | |
| C12 | Unde se completează în PixelData lateralitatea și asigurătorul privat? | chei fără câmp PixelData cunoscut | faza 2 | |
| C13 | Toate cele 10 sedii folosesc aceeași instanță PixelData? | filtrul dispatcher-ului, mapările | faza 3 | |
| C14 | Cine verifică zilnic itemii Failed din Orchestrator și îi rezolvă? | eșecurile Business nu se reîncearcă | producție | |
| C15 | Cât timp trebuie păstrați itemii din coadă (decizie GDPR)? | date de sănătate în UiPath Cloud | producție | |

## PixelData SRL

| ID | Întrebare | De ce contează | Blochează | Răspuns |
|---|---|---|---|---|
| P1 | PixelData RIS acceptă programări inbound prin HL7 v2 (SIU^S12) și pacienți (ADT^A04/A08), sau ORM^O01? Cu ce cost și licență? | alternativa fără robot ([09](09-licente-gdpr-riscuri.md) §5) | faza 4 | |
| P2 | Există import prin bază de date sau web service? | altă alternativă fără interfață | faza 4 | |
| P3 | Un user PixelData dedicat robotului poate fi logat concurent cu alte sesiuni? Licența permite un user tehnic și automatizarea interfeței? | setup laptop, assetul `PixelData_RobotLogin`, R9 | faza 2 | |
| P4 | Pe ce tehnologie e construită interfața (Delphi?) și ce expune pentru accesibilitate (UI Automation, Active Accessibility)? | alegerea selectorilor și a metodei de input | faza 2, captura UI | |
| P5 | Cum se anunță actualizările care schimbă ecranele? Există un mediu de test? | R4 | producție | |
| P6 | Ce returnează căutarea „Nume/CNP” când mai mulți pacienți au același nume? | `PATIENT_AMBIGUOUS` | faza 2 | |

## UiPath

| ID | Întrebare | De ce contează | Blochează | Răspuns |
|---|---|---|---|---|
| U1 | În ce regiune stau datele tenantului (data residency)? | GDPR | producție | |
| U2 | Cât se păstrează itemii din coadă și se poate configura retenția per coadă? | GDPR; decizia C15 | producție | |
| U3 | Cum se încheie DPA-ul pentru Automation Cloud? | GDPR | producție | |
| U4 | Ce plan plătit minim acoperă 1 Unattended în producție (Basic?) și la ce preț? Ce se întâmplă cu coada, procesul și triggerul la expirarea trial-ului (2026-11-09)? | R7 | producție | |
| U5 | Care e rolul minim de folder pentru aplicația externă a dispatcher-ului (Queues View + Transactions Create/View)? | securitate | faza 3 | **2026-09-11, răspuns.** Permisiuni per endpoint: AddQueueItem cere `Queues.View` + `Transactions.Create`; GetQueueItems (și cel pe `Id`) cere `Queues.View` + `Transactions.View`. Deci dispatcher-ul are nevoie de `Queues.View` + `Transactions.Create` + `Transactions.View`. Recuperarea prin ștergere (DeleteQueueItems, DeleteBulk) mai cere `Transactions.Delete`, iar marcarea `Retried` (SetItemReviewStatus) mai cere `Transactions.Edit` — acestea se dau operatorului, nu aplicației externe. [Sursă](https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/permissions-per-endpoint) |
| U6 | Ce cod HTTP și ce mesaj întoarce AddQueueItem la „Duplicate Reference”? (se poate afla și cu `tools/queue-client`) | dispatcher-ul tratează duplicatul ca `sent` | faza 3 | **2026-09-11, parțial.** Mesajul: da. Codul de eroare UiPath este `errorCode` `1016 DuplicateReference`, adică `Reference`-ul e deja folosit în coadă; mesajul exact, citat, și sursa: [06](06-setup-orchestrator.md) §11. Codul HTTP nu e documentat oficial (o sursă de forum spune 409) — rămâne de verificat cu `tools/queue-client`. |
| U7 | Cu „Enforce unique references” activ, se poate adăuga din nou un item cu același `Reference` după ce cel vechi e Failed sau șters? | retrimiterea după un eșec Business corectat | producție | **2026-09-11, răspuns.** Unicitatea „applies to all transactions except deleted or retried ones” ([sursă](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-queues-and-transactions)), deci un item `Failed` blochează în continuare același `Reference`; exceptați sunt doar `Deleted` și `Retried`. Itemii se pot șterge manual „no matter their status”, iar cel șters rămâne vizibil, marcat `Deleted` ([sursă](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/queue-item-statuses)). Concluzie: da, după ștergere; nu, cât timp itemul `Failed` există. |

Notă de cercetare 2026-09-11 (docs.uipath.com), pentru U6, U7 și S8; înlocuiește nota din 2026-09-10, care presupunea greșit că despre itemii `Failed` nu se spune nimic:
- „Transaction references can also be enforced to be unique, at queue level. This feature is enabled when creating the queue and applies to all transactions except deleted or retried ones” ([about-queues-and-transactions](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-queues-and-transactions)). Itemii `Failed` nu sunt exceptați.
- „Queue items can be manually deleted, no matter their status”; itemul rămâne vizibil, cu status `Deleted`. `Retried` = „the item has been marked manually for retry. As a result, a new queue item with the New status is created”. `Verified` = „Items cannot be retried after the user sets this status” ([queue-item-statuses](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/queue-item-statuses)).
- Editarea datelor unui item eșuat e documentată explicit, cu scopul „editing the data input for failed transactions before retrying them”: Edit pe itemi `New`, `Failed` sau `Abandoned`, Specific Data prin descărcare/încărcare JSON, fără tablouri ([editing-transactions](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/editing-transactions)).

## Echipa ScanExpert

| ID | Întrebare | De ce contează | Blochează | Răspuns |
|---|---|---|---|---|
| S1 | În faza 3, corespondența sucursală + modalitate → resursă PixelData rămâne în fișierul robotului sau trece în backoffice? | un singur loc de întreținut | faza 3 | |
| S2 | Se adaugă în `appointments` coloane pentru seria și numărul biletului? Din ce sursă se completează? | `ReferralNumber` | faza 3 | |
| S3 | Se trimit în PixelData programările de la toți cei 4 scriitori (recepție, backoffice, aplicația pacient, intents)? | acoperirea triggerului | faza 3 | |
| S4 | Payload-ul se construiește la trimitere (date curente) sau instantaneu la INSERT? | ce ajunge în PixelData după o editare rapidă | faza 3 | |
| S5 | Unde vede operatorul rezultatul robotului (eșecuri, coduri) și cine e alertat la rânduri `failed`? | eșecurile nu rămân nevăzute | faza 3 | |
| S6 | Laptopul poate ajunge la URL-ul recepției pentru callback? Se folosește un token separat pentru robot? | callback-ul de status | faza 3 | |
| S7 | Ce set hel se folosește în producție și cine deține secretele UiPath? | configurare | faza 3, producție | |
| S8 | Cum se retrimite o programare după corectarea unui eșec Business (împreună cu U7)? | fluxul operatorului | faza 3 | **2026-09-11, decis.** Întâi: se editează Specific Data al itemului `Failed`, apoi se marchează `Retried`, ceea ce creează un item nou cu status `New`. Când nu se poate (itemul e deja `Verified`, stare fără întoarcere, sau corectura s-a făcut în recepție, nu în item): se șterge itemul `Failed` și se retrimite același `Reference`. Un `Reference` cu sufix e respins. Procedura: [05](05-integrare-receptie.md) §6 și [06](06-setup-orchestrator.md) §10. Rămâne de verificat, la suportul UiPath: dacă itemul creat de Retry poartă datele editate sau pe cele vechi, ce `Reference` primește și în ce status rămâne părintele. |
| S9 | Se păstrează numele și prenumele separat în `customers`? | euristica „primul cuvânt = Nume” greșește la nume compuse | faza 3 | |
