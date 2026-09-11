# 06. Setup Orchestrator (UiPath Automation Cloud)

Ghid pas cu pas pentru configurarea Orchestrator, astfel încât fiecare programare pusă în coadă să pornească robotul de pe laptop.

Cum se citește:
- Fiecare pas are o linie „De ce”, ca să știi ce se strică dacă îl sari.
- „(de verificat)” = informație neconfirmată încă în interfața reală. Verifică și corectează documentul.
- Numele din `code` se scriu exact așa (sursa: [decizii-design-v1.md](sources/decizii-design-v1.md)).
- Placeholdere: `{org}` = numele organizației din URL, `{tenant}` = numele tenantului (acum `DefaultTenant`), `<client-id>` și `<client-secret>` = valorile generate de Orchestrator. Nu scrie niciodată valorile reale în fișiere din git.

Documente legate:
- [07-setup-laptop.md](07-setup-laptop.md): pregătirea laptopului Windows și conectarea robotului
- [08-construire-in-studio.md](08-construire-in-studio.md): proiectul `PixelDataProgramari` în Studio și publicarea lui
- [09-licente-gdpr-riscuri.md](09-licente-gdpr-riscuri.md): licențe, date ale pacienților, riscuri

Harta resurselor din folderul `PixelData`:

| Resursă | Nume | Pas |
|---|---|---|
| Folder | `PixelData` | §1 |
| Coadă | `PixelData_Programari` | §2 |
| Credential asset | `PixelData_RobotLogin` | §3 |
| Machine template | `PixelData-Laptop` | §4 |
| Robot account | `robot-pixeldata` | §5 |
| Proces | `PixelDataProgramari` | §6 |
| Queue trigger | `PixelData_Programari_OnNewItem` | §7 |
| External Application | `ScanExpert-Receptie-Dispatcher` | §8 |

## 0. Înainte de a începe

1. Verifică licența pentru Studio a contului tău.
   - Varianta A: Admin > Accounts & Groups > grupul **Automation Developers** > adaugă utilizatorul tău (de verificat: calea exactă din meniu).
   - Varianta B: alocă direct utilizatorului o licență **Pro** (Admin > Licenses > Users, de verificat).
   - Test: Studio Web nu mai afișează „Unlicensed”. La 2026-09-10 afișa „Unlicensed”, probabil fiindcă utilizatorul nu era în grup (de verificat).
   - De ce: fără licență de developer, Studio nu poate publica pachetul din §6.
2. Reține limitele contului.
   - Planul actual este **Standard Trial**, expiră la **2026-11-09** și este **non-producție**.
   - Pentru producție (programări reale, la clinică) e nevoie de un plan plătit. Detalii: [09-licente-gdpr-riscuri.md](09-licente-gdpr-riscuri.md).
   - Licențele Community sunt doar pentru uz necomercial; nu le folosi aici.
   - De ce: după expirare, contul trece pe Free, iar robotul se poate opri fără avertisment.
3. Respectă ordinea.
   - **Acum**, fără laptop: §1, §2, §3. Tot acum se poate face și §8 (nu depinde de laptop).
   - **După ce laptopul e gata** ([07-setup-laptop.md](07-setup-laptop.md)): §4, §5, §6, §7.
   - **La final**: §9 (smoke test), §10 (monitorizare).
   - De ce: secretul mașinii (§4) se afișează o singură dată și îl folosești imediat la conectare; procesul (§6) există doar după publicarea din Studio.

## 1. Folderul `PixelData`

1. Orchestrator > Tenant > **Folders** > **Add Folder** (de verificat: eticheta butonului).
   - Nume: `PixelData`. Folder modern, fără subfoldere. În Automation Cloud, folderele noi sunt moderne (de verificat).
   - De ce: folderul ține la un loc coada, robotul, procesul și triggerul; permisiunile se dau per folder.
2. Nu folosi `Shared` sau `My Workspace`.
   - De ce: altfel robotul și aplicația externă primesc acces la tot ce mai e acolo.
3. Dă rolurile în folder (Folder > **Assign account/group**, de verificat):

| Cine | Rol în folder | De ce |
|---|---|---|
| Contul tău (dezvoltator) | Folder Administrator (de verificat: numele exact al rolului) | Creezi și modifici coada, procesul, triggerul |
| Robot account `robot-pixeldata` (§5) | Automation User | Poate rula procesul și citi coada și assetul |
| External Application `ScanExpert-Receptie-Dispatcher` (§8) | Rol custom: `Queues.View` + `Transactions.Create` + `Transactions.View` (minimul documentat, §8) | Poate doar adăuga itemi și citi starea lor |
| Operatorul care rezolvă itemii `Failed` (§10) | În plus față de rolul lui: `Transactions.Edit` (marcare `Retried`) și `Transactions.Delete` (ștergerea unui item) | Recuperarea unui eșec Business |

## 2. Coada `PixelData_Programari`

1. Folderul `PixelData` > **Queues** > **Add queue** (de verificat).
   - Nume: `PixelData_Programari`.
2. **Enforce unique references** = ON.
   - De ce: fiecare item are Reference `create-<AppointmentId>`. A doua trimitere a aceleiași programări este respinsă („Duplicate Reference”, `errorCode` `1016`), deci robotul nu creează o programare dublă în PixelData.
   - Bifeaz-o de la început: documentația spune „This feature is enabled when creating the queue” ([about-queues-and-transactions](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-queues-and-transactions)); dacă se poate schimba și după, nu e documentat.
   - Unicitatea „applies to all transactions except deleted or retried ones”: un item `Failed` blochează în continuare același Reference. Consecința pentru operator: §10, „Recuperarea unui item `Failed`”.
3. **Auto retry** = ON, **Max # of retries** = `1`.
   - De ce: o eroare de sistem (PixelData blocat, timeout UI) primește încă o încercare. Erorile de business (date greșite) nu se reîncearcă, oricum.
4. **Nu încărca schemele JSON în coadă** (hotărât 2026-09-11).
   - Orchestrator validează datele itemului cu **JSON Schema draft-07** și acceptă doar un set închis de cuvinte-cheie: în rădăcină `type: "object"`, `properties`, `$schema`, `required`, `additionalProperties`, `$id`, `title`; pe fiecare proprietate `type`, `default`, `examples`, `pattern`, `minimum`, `maximum`, `minLength`, `maxLength`, `$id`, `title`. Tipuri permise pe proprietate: string, integer, boolean, number, object. Schema „must not contain an array”. Sursă: [about-queues-and-transactions, „Schema definitions”](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-queues-and-transactions#schema-definitions).
   - Contractele noastre nu încap în setul acela. [`contracts/appointment-queue-item.v1.schema.json`](../contracts/appointment-queue-item.v1.schema.json) declară draft 2020-12 și folosește `$defs`, `$ref`, `allOf`, `enum`, `const` și `description`; [`contracts/queue-item-output.v1.schema.json`](../contracts/queue-item-output.v1.schema.json) folosește `enum`, `const` și `description`. Aproape toate regulile utile (listele închise `Payer` și `Laterality`, `Insurer` obligatoriu la `Asigurator privat`, formatele reutilizate) stau tocmai în cuvintele-cheie nesuportate.
   - Nu e documentat dacă Orchestrator refuză fișierul sau ignoră tăcut un cuvânt-cheie nesuportat. Al doilea caz e mai rău decât lipsa validării: o coadă care pare validată și nu verifică nimic. De aceea nu se experimentează pe coada reală.
   - Un item care nu respectă schema din coadă „fails with a Business Exception” — o excepție fără codurile noastre (§10), deci strict mai puțin utilă decât ce avem. Validarea rămâne unde e: regulile robotului (`MISSING_FIELD`, `INVALID_FIELD`, `CNP_INVALID`, …) și `python3 contracts/validate_examples.py` pentru exemple (docs/10 §3).
   - De reținut, dacă decizia se redeschide: schema din coadă nu se aplică retroactiv itemilor existenți, iar la fiecare versiune nouă de contract trebuie reîncărcată, altfel itemii noi sunt respinși.
5. Lasă celelalte setări implicite (fără SLA, fără Deadline).
   - De ce: deadline-ul schimbă doar prioritatea; regula „programarea e în trecut” o verifică robotul (`APPOINTMENT_IN_PAST`).

## 3. Credential asset `PixelData_RobotLogin`

1. Cere un user PixelData dedicat robotului ([07-setup-laptop.md](07-setup-laptop.md) §2).
   - De ce: în PixelData se vede ce a introdus robotul, iar parola omului nu ajunge în Orchestrator.
2. Folderul `PixelData` > **Assets** > **Add asset** (de verificat).
   - Nume: `PixelData_RobotLogin`. Tip: **Credential**.
   - Username + Password = userul PixelData al robotului.
   - Valoare globală, nu per robot (de verificat: eticheta opțiunii).
   - De ce: parola stă criptată în Orchestrator; robotul o citește în starea Init (Get Credential) și nu apare în cod, în `Config.xlsx` sau în git. Vezi [08-construire-in-studio.md](08-construire-in-studio.md).
3. Când se schimbă parola în PixelData, actualizează imediat assetul.
   - De ce: altfel fiecare item eșuează cu `PIXELDATA_LOGIN_FAILED`.

## 4. Machine template `PixelData-Laptop`

Faci pasul acesta când laptopul e gata ([07-setup-laptop.md](07-setup-laptop.md) §0–§6).

1. Orchestrator > Tenant > **Machines** > **Add machine** > **Machine template** (de verificat).
   - Nume: `PixelData-Laptop`.
   - De ce: un template nu depinde de numele Windows al laptopului; dacă schimbi laptopul, refolosești aceeași configurație.
2. Runtimes: **Unattended** = `1`. Restul = `0`.
   - De ce: tenantul are 2 licențe Unattended; laptopul folosește una. Robotul fără runtime Unattended nu poate porni joburi din trigger.
3. Salvează și copiază **Client ID** și **Client Secret**.
   - Secretul se afișează **o singură dată**.
   - Pune-le imediat într-un password manager. Niciodată în git, în chat, în email sau într-un fișier text pe desktop.
   - Dacă l-ai pierdut: generează un secret nou pe același template (de verificat: butonul exact) și reconectează robotul ([07-setup-laptop.md](07-setup-laptop.md) §7).
   - De ce: cine are Client ID + Client Secret poate conecta orice calculator ca robot în tenantul tău.

## 5. Robot account `robot-pixeldata`

1. Admin > Accounts & Groups > **Robot accounts** > **Add robot account** (de verificat).
   - Nume: `robot-pixeldata`.
   - Nu folosi `Default Robot` existent.
   - De ce: un cont dedicat se poate da doar în folderul `PixelData` și se poate dezactiva fără să afecteze altceva.
2. Secțiunea **Unattended setup**:
   - Tip joburi: **foreground and background** (de verificat: eticheta exactă).
     - De ce: PixelData e aplicație desktop și are nevoie de o sesiune Windows interactivă (foreground).
   - **Domain\Username** = contul Windows local de pe laptop: `<NUME-LAPTOP>\robot-pixeldata` (de verificat dacă merge și forma `.\robot-pixeldata`).
     - `<NUME-LAPTOP>` se află pe laptop cu comanda `hostname`.
   - **Password** = parola Windows a contului `robot-pixeldata` ([07-setup-laptop.md](07-setup-laptop.md) §1).
   - De ce: robotul deschide singur o sesiune Windows cu acest cont; fără user și parolă corecte, jobul nu pornește.
3. Dă contul în folderul `PixelData` cu rolul **Automation User** (§1).
   - De ce: fără rol în folder, triggerul nu găsește niciun robot, iar jobul rămâne Pending.
4. Adaugă machine template-ul `PixelData-Laptop` în folder (Folder > **Machines** > **Manage machines in folder**, de verificat).
   - De ce: jobul rulează doar pe o mașină atribuită folderului.
5. Regulă permanentă: când se schimbă parola Windows a contului `robot-pixeldata`, actualizeaz-o și aici.
   - De ce: altfel robotul nu mai poate deschide sesiunea (vezi [07-setup-laptop.md](07-setup-laptop.md) §11).

## 6. Procesul `PixelDataProgramari`

1. Pe laptop, în Studio: deschide proiectul `PixelDataProgramari` > **Publish** > destinația Orchestrator (feed-ul tenantului sau al folderului `PixelData`, de verificat). Pașii completi: [08-construire-in-studio.md](08-construire-in-studio.md).
   - De ce: pachetul publicat (`.nupkg`) este codul robotului; fără el nu ai ce porni.
2. Orchestrator > folderul `PixelData` > **Automations** > **Processes** > **Add process** (de verificat).
   - Package: `PixelDataProgramari`, ultima versiune.
   - Nume proces: `PixelDataProgramari`.
   - De ce: procesul leagă pachetul de folder; triggerul (§7) pornește un proces, nu un pachet.
3. La fiecare publicare nouă, treci procesul pe versiunea nouă (sau activează actualizarea automată la ultima versiune, dacă opțiunea există, de verificat).
   - De ce: altfel robotul rulează în continuare versiunea veche.

## 7. Queue trigger `PixelData_Programari_OnNewItem`

1. Folderul `PixelData` > **Automations** > **Triggers** > **Queue Triggers** > **Add** (de verificat).
   - Nume: `PixelData_Programari_OnNewItem`.
   - Process: `PixelDataProgramari`. Queue: `PixelData_Programari`.
2. **Minimum number of items to trigger the first job** = `1` (de verificat: eticheta exactă).
   - De ce: robotul pornește la primul item, fără să aștepte un lot.
3. **Maximum number of pending and running jobs allowed simultaneously** = `1`.
   - De ce: un singur job golește coada (REFramework ia item după item până nu mai sunt). Cât laptopul e oprit, nu se strâng zeci de joburi Pending; laptopul oricum are o singură sesiune pentru robot.
4. Setarea „un job nou pentru fiecare N itemi noi”: lasă valoarea implicită (de verificat).
   - De ce: limita de la pasul 3 are prioritate; nu pornește niciodată al doilea job.
5. Runtime type: **Unattended**. Contul/mașina: `robot-pixeldata` pe `PixelData-Laptop`, sau alocare dinamică în folder (de verificat: ce opțiuni oferă formularul).
   - De ce: triggerul de coadă cere runtime Unattended.
6. Activează triggerul.
   - Comportament: pornește la adăugarea unui item și reverifică coada la fiecare 30 de minute.
   - De ce: dacă laptopul a fost oprit, itemii rămași sunt preluați la următoarea verificare.

## 8. External Application `ScanExpert-Receptie-Dispatcher`

1. Admin > **External Applications** > **Add Application** (de verificat).
   - Nume: `ScanExpert-Receptie-Dispatcher`.
   - Tip: **Confidential application**.
   - De ce: aplicația confidențială se autentifică doar cu Client ID + Client Secret, fără utilizator uman și fără parola cuiva.
2. **Add scopes** > Orchestrator API Access > **Application scopes** > bifează `OR.Queues`. Nimic altceva.
   - Nu bifa User scopes.
   - De ce: tokenul poate doar lucra cu cozi; dacă secretul scapă, nu se pot porni joburi sau citi assets.
3. Salvează și copiază **App ID** (Client ID) și **App Secret**.
   - Secretul se afișează o singură dată (de verificat).
   - Păstrează-l în password manager și, pentru teste, doar în `tools/queue-client/.env` (copie după `tools/queue-client/.env.example`, vezi §9). `.env` este ignorat de git (vezi `.gitignore`). Mai târziu, secretul se mută în hel, lângă celelalte secrete ale recepției.
   - De ce: e credențialul care poate introduce programări în coadă.
4. Folderul `PixelData` > **Assign account/group** > caută `ScanExpert-Receptie-Dispatcher` > rol cu exact `Queues.View` + `Transactions.Create` + `Transactions.View`.
   - Dacă nu există un rol potrivit: Tenant > Manage Access > Roles > rol nou de folder cu doar aceste permisiuni (de verificat: calea exactă).
   - De ce atât: AddQueueItem cere `Queues.View` + `Transactions.Create`, iar GetQueueItems (reconcilierea și `queue-client status`) cere `Queues.View` + `Transactions.View`. Sursă: [permissions-per-endpoint](https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/permissions-per-endpoint).
   - Nu-i da `Transactions.Delete` sau `Transactions.Edit`: sunt permisiunile de recuperare din §10 și aparțin operatorului uman, nu unui credențial care stă într-un server.
   - De ce: scope-ul `OR.Queues` spune ce API poate apela aplicația; rolul din folder spune pe ce coadă are voie.
5. Cine folosește aplicația:
   - Acum: `tools/queue-client`, pentru teste manuale (§9).
   - Faza 2: recepția ScanExpert, prin dispatcher-ul care citește outbox-ul.
   - De ce: un singur credențial, cu drepturi minime, pentru orice pune itemi în coadă. Rotește secretul dacă a ajuns pe un calculator necontrolat.

## 9. Smoke test (primul test cap-coadă)

Condiții: §1–§8 făcute, robotul conectat ([07-setup-laptop.md](07-setup-laptop.md) §7–§8), procesul publicat (§6). Cazurile complete de test: [10-plan-teste.md](10-plan-teste.md).

Atenție: testul creează o programare **reală** în PixelData. Folosește un pacient de test stabilit cu clinica, sau un mediu de test PixelData, dacă există (de verificat). Nu folosi date reale de pacienți în exemple.

1. Pregătește `tools/queue-client` pe calculatorul de dezvoltare (bash, cu Go instalat). Detalii: [tools/queue-client/README.md](../tools/queue-client/README.md).
   ```bash
   cd tools/queue-client
   go build -o queue-client .
   cp .env.example .env
   ```
   - Completează `.env`. Toate variabilele sunt obligatorii; lista e în §11. Valorile stau între apostrofuri simple, pentru că secretul poate conține `$`.
   - Încarcă variabilele în shell, din același folder:
     ```bash
     set -a; . ./.env; set +a
     ```
   - Pe Windows, comenzile merg în WSL sau Git Bash (de verificat).
   - De ce: clientul face exact apelurile pe care le va face recepția în faza 2 (token + AddQueueItem).
2. Alege un exemplu din [`contracts/examples/valid/`](../contracts/examples/valid/). Verifică:
   - data și ora programării sunt în viitor; altfel robotul oprește itemul cu `APPOINTMENT_IN_PAST`
   - sucursala și modalitatea au mapare în `robot/Data/pixeldata-mappings.json`; altfel `RESOURCE_MAPPING_MISSING`
   - Dacă trebuie să schimbi data, lucrează pe o copie în afara repo-ului.
   - De ce: un eșec de business la primul test ascunde întrebarea reală: merge lanțul coadă > trigger > robot?
3. Pune exemplul în coadă (sau calea copiei tale de la pasul 2):
   ```bash
   ./queue-client enqueue -file ../../contracts/examples/valid/cas-cu-bilet.json
   ```
   - Rezultat așteptat: afișează `Id` și Reference `create-<AppointmentId>`, cod de ieșire `0`.
   - Cod de ieșire `1` = eroare API sau rețea. Cod `2` = configurare, argumente sau fișier greșit; mesajul numește variabila lipsă.
   - La cod `1`, reia comanda cu `-debug` (§11): afișează în plus răspunsul complet al Orchestrator, care spune de ce a refuzat itemul. Codurile `errorCode` uzuale la configurare greșită (coadă, folder, header): §11. Răspunsul citează itemul înapoi, deci poate conține date de pacient (nume, CNP, telefon): citește-l în terminal și nu-l lipi în ticket, în chat sau în log.
4. Orchestrator > folderul `PixelData` > **Queues** > `PixelData_Programari` > **View Transactions** (de verificat).
   - Itemul apare cu Reference `create-<AppointmentId>` și Status `New`.
5. Orchestrator > folderul `PixelData` > **Jobs** (de verificat: calea exactă).
   - Apare un job `PixelDataProgramari`: `Pending`, apoi `Running`.
   - Dacă rămâne `Pending`: laptop oprit sau deconectat, robot fără rol în folder, sesiune Windows blocată. Vezi [07-setup-laptop.md](07-setup-laptop.md) §9 și §11.
6. Așteaptă finalul itemului:
   - `Successful`: Output are `Outcome` = `created`
   - `Failed`: `ProcessingException` începe cu codul de eroare (§10)
7. Verifică starea prin API:
   ```bash
   ./queue-client status -reference create-<AppointmentId>
   ```
   - Afișează `Id`, `Status`, `ProcessingException` (Type, Reason, Details) și `Output`.
   - Cod de ieșire `3` = nu există item cu acest Reference.
   - De ce: așa va afla recepția rezultatul în faza 2 (reconciliere).
8. Deschide PixelData > **PROGRAMĂRI**: programarea apare la data, ora și resursa din item.
   - De ce: `Successful` în Orchestrator spune doar că robotul a terminat fără excepție; ecranul PixelData confirmă salvarea.
9. Test de duplicat: rulează din nou aceeași comandă `enqueue`.
   - Rezultat așteptat: `queue-client` afișează „already queued” și iese cu `0`. Orchestrator a respins Reference-ul duplicat cu `errorCode` `1016 DuplicateReference` (codul HTTP: de verificat, §11). Niciun item nou, niciun job nou.
   - De ce: confirmă că setarea Enforce unique references (§2) este activă.
10. Curățenie: șterge programarea de test din PixelData (și pacientul de test, dacă a fost creat și ai voie).
    - De ce: programarea de test ocupă un slot real.

## 10. Monitorizare

1. Alertă pentru itemi `Failed`: meniul utilizatorului > **Preferences** > **Notification settings** > Orchestrator > Queues (de verificat: calea și numele notificărilor). Abonează-te la eșecurile de item, pe email.
   - De ce: un item eșuat înseamnă o programare care NU e în PixelData; cineva trebuie să o introducă manual.
2. Alertă pentru joburi `Pending` prea mult (prag propus: 1 oră).
   - Mecanismul exact nu e confirmat (de verificat): SLA pe coadă, alertă din Insights, sau o verificare manuală zilnică a paginii Jobs.
   - De ce: laptop oprit sau deconectat = coada crește în liniște, fără nicio eroare.
3. Alertă pentru joburi `Faulted` și robot deconectat (de verificat: numele notificărilor).
   - De ce: un job căzut lasă itemul `InProgress` până la 24 de ore.
4. Verificare zilnică (2 minute): coada `PixelData_Programari` (câți `New`, câți `Failed`) și pagina Jobs.

Ce înseamnă stările unui item:

| Status | Ce înseamnă | Ce faci |
|---|---|---|
| `New` | Itemul așteaptă; robotul nu l-a luat încă | Nimic. Dacă stă peste 1 oră: verifică laptopul și jobul |
| `InProgress` | Robotul lucrează la el acum | Nimic. Dacă stă ore întregi: jobul a căzut (vezi `Abandoned`) |
| `Successful` | Programarea e în PixelData. Output `Outcome`: `created` (creată acum) sau `already_existed` (exista deja) | Nimic |
| `Failed` | Robotul a aruncat excepție. Business (date greșite): fără retry. Application (sistem): retry automat, dacă mai are încercări | Citește codul din `ProcessingException` |
| `Abandoned` | A stat `InProgress` 24 de ore fără final (laptop căzut sau oprit în timpul jobului) | Verifică în PixelData dacă programarea există. Auto retry îl reia; robotul verifică întâi „există deja?” |
| `Retried` | S-a creat un item nou, cu status `New`. Fie automat (Auto retry după o excepție system; copia păstrează Reference-ul), fie manual: „the item has been marked manually for retry” | Uită-te la itemul nou. La retry automat e cel cu același Reference și Id-ul cel mai mare; ce Reference primește itemul creat de un retry manual: de verificat |
| `Deleted` | Cineva a șters itemul. Ștergerea e permisă „no matter their status”, iar itemul rămâne vizibil în listă | Nimic. Reference-ul lui e liber: se poate trimite din nou aceeași programare |
| `Verified` | Cineva a marcat itemul ca verificat. Stare fără întoarcere: „Items cannot be retried after the user sets this status” | Dacă mai trebuie reluat, singura cale rămasă e ștergerea itemului și retrimiterea |

Unde vezi codul de eroare:
- Orchestrator: coada `PixelData_Programari` > View Transactions > click pe item > detaliile excepției: tip (Business/Application) și Reason (de verificat: etichetele).
- API: câmpul `ProcessingException` din răspunsul QueueItems (§11). `./queue-client status -reference create-<AppointmentId>` îl afișează (Type, Reason, Details). Trimite mereu headerul de folder: „QueueItems endpoints count and return the DTO for every recurrence of a queue item in all the folders the queue is linked to” (docs.uipath.com, ghidul de API Orchestrator; pagina exactă de verificat), deci o căutare după Reference fără `X-UIPATH-FolderPath` întoarce câte un rând pentru fiecare folder în care e legată coada, iar „Id-ul cel mai mare” nu mai înseamnă nimic. `queue-client` îl trimite (§11).
- Mesajul începe mereu cu codul, de ex. `CNP_INVALID: <mesaj>`.
- Pentru excepțiile Application, pe laptop există și o captură de ecran în `Exceptions_Screenshots` ([07-setup-laptop.md](07-setup-laptop.md) §10).

| Tip | Coduri | Ce faci |
|---|---|---|
| Business (datele) | `MISSING_FIELD`, `INVALID_FIELD`, `UNSUPPORTED_SCHEMA_VERSION`, `UNSUPPORTED_OPERATION`, `CNP_REQUIRED`, `CNP_INVALID`, `APPOINTMENT_IN_PAST`, `RESOURCE_MAPPING_MISSING`, `PATIENT_SOURCE_MAPPING_MISSING`, `PATIENT_AMBIGUOUS`, `SLOT_OCCUPIED`, `PIXELDATA_CNP_REJECTED` | Corectează datele (în recepție sau în fișierul de mapări), apoi reia itemul după procedura de mai jos. |
| System (mediul) | `PIXELDATA_UNAVAILABLE`, `PIXELDATA_LOGIN_FAILED`, `PIXELDATA_UI_TIMEOUT`, `PIXELDATA_SAVE_UNCONFIRMED` | Verifică laptopul și PixelData ([07-setup-laptop.md](07-setup-laptop.md) §11). La `PIXELDATA_SAVE_UNCONFIRMED` verifică în PixelData dacă programarea s-a salvat. Apoi retry manual, la fel ca mai jos (marcare `Retried`, fără editare). |

Lista completă și regulile: [decizii-design-v1.md](sources/decizii-design-v1.md), secțiunea „Coduri de eroare inițiale”.

**Recuperarea unui item `Failed`**

Un item `Failed` NU eliberează Reference-ul: unicitatea „applies to all transactions except deleted or retried ones”, iar un `AddQueueItem` cu același Reference primește tot `1016 DuplicateReference`. Deci nu retrimite din recepție înainte de a face unul din cei doi pași de mai jos.

Varianta 1 — datele se pot corecta chiar în item (mapare greșită, o valoare greșită în `SpecificContent`):
1. Coada `PixelData_Programari` > View Transactions > itemul `Failed` > **Edit** (disponibil pe itemi `New`, `Failed` și `Abandoned`; Clone merge și pe `Successful`).
2. Specific Data se editează prin descărcarea fișierului JSON, modificare, apoi încărcare. Nu poate conține tablouri; contractul nostru are oricum chei plate, deci nu e o problemă ([03-contract-coada.md](03-contract-coada.md) §2).
3. Marchează itemul **Retried**. Orchestrator creează un item nou, cu status `New`, iar triggerul îl ia în lucru.
4. Cere `Transactions.Edit` în folder (§1).

Varianta 2 — itemul nu se mai poate relua (e deja `Verified`) sau corectura s-a făcut în recepție, nu în item:
1. Șterge itemul `Failed` (ștergerea e permisă indiferent de status; itemul rămâne vizibil, marcat `Deleted`, și nu mai blochează Reference-ul).
2. Retrimite programarea sub același Reference `create-<AppointmentId>` — din recepție în faza 3, sau cu `./queue-client enqueue` acum.
3. Cere `Transactions.Delete` în folder (§1).

Nu inventa un Reference nou (`create-<AppointmentId>-r2` sau altceva): Reference-ul e cheia de idempotență a programării, iar cu sufix o trimitere accidentală nu ar mai fi respinsă de coadă. Vezi [05-integrare-receptie.md](05-integrare-receptie.md) §6.

De verificat la prima recuperare reală (întrebare pentru suportul UiPath, [11](11-intrebari-deschise.md) S8): dacă itemul creat de Retry poartă datele editate sau pe cele vechi, ce Reference primește și în ce status rămâne părintele.

Surse: [about-queues-and-transactions](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-queues-and-transactions), [queue-item-statuses](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/queue-item-statuses), [editing-transactions](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/editing-transactions).

## 11. Referință API

Variabilele de mediu `tools/queue-client`. Toate sunt obligatorii, fără valori implicite: una lipsă sau goală dă o eroare care numește variabila (cod de ieșire `2`). Șablon: `tools/queue-client/.env.example`. Detalii: [tools/queue-client/README.md](../tools/queue-client/README.md).

| Variabilă | Valoare | Legătura cu Orchestrator |
|---|---|---|
| `UIPATH_CLOUD_URL` | `https://cloud.uipath.com` (doar schema și hostul, fără org) | Baza URL-urilor de mai jos |
| `UIPATH_ORG` | `{org}` | Numele organizației din URL |
| `UIPATH_TENANT` | `{tenant}` (acum `DefaultTenant`) | Numele tenantului |
| `UIPATH_CLIENT_ID` | `<client-id>` | App ID al `ScanExpert-Receptie-Dispatcher` (§8) |
| `UIPATH_CLIENT_SECRET` | `<client-secret>` | App Secret al `ScanExpert-Receptie-Dispatcher` (§8) |
| `UIPATH_FOLDER_PATH` | `PixelData` | Trimis ca header `X-UIPATH-FolderPath` |
| `UIPATH_QUEUE_NAME` | `PixelData_Programari` | `Name` din body-ul AddQueueItem |

Scope-ul nu e variabilă: `queue-client` cere mereu `OR.Queues`.

Ambele comenzi, `enqueue` și `status`, acceptă `-debug`: pe lângă mesajul obișnuit de eroare (care numește doar operația și codul HTTP) afișează și corpul răspunsului de eroare al Orchestrator. Este oprit implicit pentru că acel corp citează itemul înapoi, cu date de pacient în el (nume, CNP, telefon); pornește-l doar cât depanezi și nu-l lipi în ticket, în chat sau în log.

| Ce | Valoare |
|---|---|
| Token | `POST https://cloud.uipath.com/{org}/identity_/connect/token` |
| Body token | form: `grant_type=client_credentials&client_id=<client-id>&client_secret=<client-secret>&scope=OR.Queues` |
| Răspuns token | `access_token`, `expires_in` = `3600`. Fără refresh token: păstrezi tokenul și ceri altul la expirare |
| Bază Orchestrator (`{base}`) | `https://cloud.uipath.com/{org}/{tenant}/orchestrator_` |
| Adăugare item | `POST {base}/odata/Queues/UiPathODataSvc.AddQueueItem` |
| Body adăugare | `{"itemData":{"Name":"PixelData_Programari","Priority":"Normal","Reference":"create-<AppointmentId>","SpecificContent":{...}}}` |
| Răspuns adăugare | `201` + itemul (Status `New`). Reference duplicat: respins cu `errorCode` `1016 DuplicateReference` (mesajul exact: tabelul de coduri de mai jos); codul HTTP nu e documentat oficial (o sursă de forum spune `409`) — de verificat |
| Stare item | `GET {base}/odata/QueueItems?$filter=Reference eq 'create-<AppointmentId>'&$orderby=Id desc&$top=1` |
| Câmpuri utile | `Status`, `Output`, `ProcessingException`. Contează itemul cu cel mai mare `Id` (retry-urile păstrează Reference) |
| Headere | `Authorization: Bearer <token>`, `Content-Type: application/json` |
| Header folder (unul din două) | `X-UIPATH-FolderPath: PixelData` sau `X-UIPATH-OrganizationUnitId: <folder-id>` |
| Scope | `OR.Queues` (de verificat: dacă ajunge și pentru citirea QueueItems fără alt scope) |
| Limite | Reference: cele 128 de caractere și interdicția apostrofului sunt documentate pentru **expresia de filtrare**, nu pentru câmpul stocat ([get-queue-items](https://docs.uipath.com/activities/other/latest/workflow/get-queue-items)); o sursă de forum spune că și câmpul refuză peste 128 (de verificat). Al nostru are 43. SpecificContent: maximum „256,000 characters or 512,000 bytes” ([about-queues-and-transactions](https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-queues-and-transactions#schema-definitions)) |

Note:
- `X-UIPATH-FolderPath` nu cere ID-ul folderului. ID-ul numeric pentru `X-UIPATH-OrganizationUnitId` apare în URL-ul Orchestrator când deschizi folderul (de verificat: parametrul exact).
- Headerul de folder nu e opțional nici la citire: fără el, endpointurile QueueItems întorc itemul o dată pentru fiecare folder în care e legată coada (§10).
- Permisiuni per endpoint: AddQueueItem = `Queues.View` + `Transactions.Create`; GetQueueItems = `Queues.View` + `Transactions.View`; DeleteQueueItems / DeleteBulk = `Queues.View` + `Transactions.Delete`; SetItemReviewStatus (marcarea `Retried`) = `Queues.View` + `Transactions.Edit` ([permissions-per-endpoint](https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/permissions-per-endpoint)).
- În `curl`, spațiile și apostrofurile din `$filter` trebuie codate în URL (`%20`, `%27`).

Coduri de eroare Orchestrator, din câmpul `errorCode` al răspunsului ([response-codes](https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/response-codes)). Sunt cele care apar la configurare greșită; le vezi cu `-debug`:

| `errorCode` | Documentat ca | La noi înseamnă |
|---|---|---|
| `1016 DuplicateReference` | „Error creating [ReferenceName]. Duplicate Reference.” | Programarea e deja în coadă. Emitentul o tratează ca trimisă; dacă itemul vechi e `Failed`, vezi §10 |
| `1002 ItemNotFound` | resursa nu există (pagina numește explicit cozile și itemii de coadă) | `UIPATH_QUEUE_NAME` greșit, sau coada nu există în folderul cerut |
| `1100 InvalidOrganizationUnit` | apelantul e asociat cu alt folder decât cel accesat | folder greșit în `X-UIPATH-FolderPath` / `X-UIPATH-OrganizationUnitId` |
| `1101 RequiredOrganizationUnit` | POST fără parametrul de folder | headerul de folder lipsește cu totul |
| `1850 TransactionReferenceRequired` | `Reference` lipsă pe o coadă cu unicitate impusă | emitentul a trimis un item fără `Reference`; la noi e mereu `create-<AppointmentId>` |

Două lucruri NU sunt documentate, ca să nu le cauți: nu există un cod de eroare pentru „apelantul nu are drepturi în acest folder” (surse de forum spun un `403` simplu, neverificat), iar forma corpului de eroare nu e documentată deloc — niciun exemplu JSON, niciun nume de câmp. `1017 ForbiddenOperation` și `1102 OrganizationUnitNotEditable` sună potrivit, dar sunt documentate pe endpointuri fără legătură cu cozile.

Surse UiPath:
- https://docs.uipath.com/automation-cloud/automation-cloud/latest/api-guide/accessing-uipath-resources-using-external-applications
- https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/transactions-requests
- https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/building-api-requests
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/managing-queues-in-orchestrator
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/queue-triggers
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/about-queues-and-transactions (unicitatea Reference-ului, schemele cozii, limita SpecificContent)
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/queue-item-statuses (statusuri, ștergere, `Retried`, `Verified`)
- https://docs.uipath.com/orchestrator/automation-cloud/latest/user-guide/editing-transactions (Edit și Clone pe un item)
- https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/response-codes (codurile `errorCode`: 1016, 1002, 1100, 1101, 1850)
- https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/permissions-per-endpoint (permisiunile minime per endpoint)

## 12. Checklist final

Acum (fără laptop):
- [ ] Contul meu are licență de Studio (grup Automation Developers sau Pro direct)
- [ ] Știu că trialul expiră la 2026-11-09 și nu e pentru producție
- [ ] Folderul `PixelData` există, cu rolurile din §1
- [ ] Coada `PixelData_Programari`: Enforce unique references = ON, Auto retry = ON, Max # of retries = 1
- [ ] Schemele JSON NU sunt încărcate în coadă (§2 pas 4); validarea rămâne în robot
- [ ] Assetul Credential `PixelData_RobotLogin` există, cu userul PixelData al robotului
- [ ] External Application `ScanExpert-Receptie-Dispatcher`: Confidential, doar `OR.Queues`, adăugată în folder cu exact `Queues.View` + `Transactions.Create` + `Transactions.View`
- [ ] Secretul aplicației externe e în password manager și în `tools/queue-client/.env`, nicăieri altundeva

După laptop:
- [ ] Machine template `PixelData-Laptop`, 1 runtime Unattended, secret salvat în password manager
- [ ] Robot account `robot-pixeldata`: foreground + background, `<NUME-LAPTOP>\robot-pixeldata` + parolă, rol Automation User în `PixelData`
- [ ] Machine template `PixelData-Laptop` atribuit folderului `PixelData`
- [ ] Robotul apare conectat ([07-setup-laptop.md](07-setup-laptop.md) §7)
- [ ] Procesul `PixelDataProgramari` adăugat în folder, pe ultima versiune
- [ ] Triggerul `PixelData_Programari_OnNewItem`: minimum 1 item, maximum 1 job pending și running, runtime Unattended, activ

La final:
- [ ] Smoke test §9 trecut: `New` > job > `Successful` > programarea vizibilă în PixelData
- [ ] Testul de duplicat respins
- [ ] Programarea de test ștearsă din PixelData
- [ ] Alertele din §10 configurate (sau verificarea zilnică stabilită)
- [ ] Toate „(de verificat)” din acest document confirmate sau corectate
