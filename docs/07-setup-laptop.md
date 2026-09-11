# 07. Setup laptop Windows (robot PixelData)

Ghid pas cu pas pentru laptopul Windows care are PixelData și pe care rulează robotul Unattended.

Cum se citește:
- Fiecare pas are o linie „De ce”.
- „(de verificat)” = informație neconfirmată pe laptopul real. Verifică și corectează documentul.
- Numele din interfața Windows sunt date în engleză. Dacă Windows e în română, etichetele apar traduse (de verificat: denumirile exacte).
- Placeholdere: `{org}`, `{tenant}`, `<client-id>`, `<client-secret>`, `<NUME-LAPTOP>`. Nu scrie valorile reale în fișiere din git.

Documente legate:
- [06-setup-orchestrator.md](06-setup-orchestrator.md): folder, coadă, machine template, robot account, trigger
- [08-construire-in-studio.md](08-construire-in-studio.md): proiectul din Studio, captura UI PixelData
- [09-licente-gdpr-riscuri.md](09-licente-gdpr-riscuri.md): date ale pacienților, riscuri

Ordinea recomandată:
1. Laptop: §0–§6 (acest document).
2. Orchestrator: [06-setup-orchestrator.md](06-setup-orchestrator.md) §4–§5 (machine template, robot account).
3. Laptop: §7–§8 (conectare, setări robot).
4. Studio și publicare: [08-construire-in-studio.md](08-construire-in-studio.md), apoi [06-setup-orchestrator.md](06-setup-orchestrator.md) §6–§9.

## 0. Verificări înainte de a începe

1. Află ediția Windows: `Win+R` > `winver` (sau Settings > System > About).

| Ediție | Mod de lucru robot | Ce înseamnă |
|---|---|---|
| Windows 10/11 **Pro** (sau Enterprise, Education) | **RDP mode** (`Login To Console` = No) | Robotul își deschide propria sesiune Remote Desktop pe laptop. Ecranul fizic nu arată ce face robotul. |
| Windows 10/11 **Home** | **Console mode** (`Login To Console` = Yes) | Home nu poate fi gazdă RDP. Robotul se loghează direct pe ecranul fizic, ca un om la tastatură. Oricine stă lângă laptop vede ce face robotul, inclusiv datele pacienților. |

   - De ce: modul decide setările din §1, §5 și §8. Pe Home ai mai multe restricții și un risc de confidențialitate.
2. Verifică drepturile de administrator pe contul tău Windows.
   - De ce: instalarea pentru toți utilizatorii (§6), politicile de grup (§5) și crearea contului robot (§1) cer admin.
3. Verifică faptul că PixelData e instalat și funcționează cu un user uman.
   - Notează calea executabilului (click dreapta pe scurtătură > Properties > Target).
   - De ce: robotul pornește PixelData după această cale.
4. Notează rezoluția și scalarea actuale: Settings > System > Display > **Display resolution** și **Scale**.
   - De ce: le vei fixa în §4 și §8; dacă se schimbă ulterior, robotul nu mai găsește elementele din ecran.
5. Asigură-te că laptopul are alimentare permanentă și internet stabil.
   - De ce: robotul trebuie să vorbească permanent cu Orchestrator; fără heartbeat 2 minute, e considerat deconectat.

## 1. Contul Windows local `robot-pixeldata`

1. Creează un cont **local** (nu cont Microsoft), cu **parolă** (nu PIN) (de verificat: dacă un cont Microsoft sau PIN-ul pot funcționa).
   - Varianta simplă, din PowerShell pornit ca administrator:
     ```powershell
     net user robot-pixeldata * /add
     ```
     Comanda cere parola de la tastatură, deci parola nu rămâne în istoricul PowerShell.
   - Varianta din interfață: Settings > Accounts > Other users > Add account > „I don't have this person's sign-in information” > „Add a user without a Microsoft account” (de verificat: etichetele din Windows 11).
   - De ce: Orchestrator se loghează cu `<NUME-LAPTOP>\robot-pixeldata` + parolă; un PIN sau un cont Microsoft nu merg cu această metodă.
2. Lasă contul **Standard user**, nu administrator (de verificat: dacă PixelData cere drepturi de admin la pornire).
   - De ce: dacă cineva preia sesiunea robotului, nu poate modifica sistemul.
3. Setează parola să nu expire:
   ```powershell
   Set-LocalUser -Name robot-pixeldata -PasswordNeverExpires $true
   ```
   - De ce: o parolă expirată oprește robotul fără nicio alertă clară (vezi §11). Dacă o schimbi manual, actualizeaz-o și în Orchestrator ([06-setup-orchestrator.md](06-setup-orchestrator.md) §5).
4. Doar pe **Pro**: adaugă contul în grupul **Remote Desktop Users**:
   ```powershell
   net localgroup "Remote Desktop Users" robot-pixeldata /add
   ```
   - Pe Windows în română, grupul are nume tradus (de verificat: „Utilizatori desktop la distanță”).
   - Tot pe Pro, activează Remote Desktop: Settings > System > Remote Desktop = On (de verificat: dacă e necesar pentru RDP mode).
   - De ce: în RDP mode robotul deschide o sesiune Remote Desktop; fără grup, Windows o refuză.
5. Loghează-te o dată manual ca `robot-pixeldata`:
   - pornește PixelData și închide orice fereastră de prima rulare
   - aplică setările din §3 (Focus assist) și §4 (afișaj)
   - apoi **Sign out** (nu Lock)
   - De ce: la prima logare Windows creează profilul și afișează ecrane de bun venit; robotul nu știe să treacă de ele.
6. Notează `<NUME-LAPTOP>`: comanda `hostname`.
   - De ce: îl folosești la Domain\Username în [06-setup-orchestrator.md](06-setup-orchestrator.md) §5.

## 2. Userul PixelData al robotului

1. Cere administratorului PixelData (sau producătorului) un user dedicat robotului.
   - Drepturi: căutare pacienți, creare pacienți („Date demografice”), creare programări. Nimic în plus.
   - De ce: în PixelData se vede ce a introdus robotul; dacă ceva merge prost, user-ul se poate bloca fără să oprească oamenii.
2. Întreabă producătorul:
   - Poate același user să fie logat în două locuri simultan? Ce se întâmplă cu prima sesiune?
   - Parola userului expiră? La ce interval?
   - Userul consumă o licență PixelData în plus?
   - De ce: dacă un om folosește același user în timpul unui job, PixelData poate închide sesiunea robotului (`PIXELDATA_LOGIN_FAILED`).
3. Nu folosi niciodată acest user ca om.
   - De ce: o logare umană poate da afară robotul sau poate schimba setări de ecran salvate pe user.
4. Pune user-ul și parola doar în assetul `PixelData_RobotLogin` ([06-setup-orchestrator.md](06-setup-orchestrator.md) §3).
   - De ce: robotul le citește de acolo; nicio copie pe hârtie, în fișiere sau în proiect.

## 3. Alimentare, capac, actualizări, notificări

1. Pe alimentare, laptopul nu intră niciodată în Sleep: Settings > System > Power & battery > Screen and sleep > „When plugged in, put my device to sleep after” = **Never**. Sau, din PowerShell ca administrator:
   ```powershell
   powercfg /change standby-timeout-ac 0
   powercfg /change hibernate-timeout-ac 0
   ```
   - De ce: un laptop adormit nu trimite heartbeat; Orchestrator îl vede deconectat, iar joburile rămân `Pending`.
2. Capac închis pe alimentare = **Do nothing**: Control Panel > Power Options > „Choose what closing the lid does” > Plugged in = Do nothing. Sau:
   ```powershell
   powercfg /setacvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 0
   powercfg /setactive SCHEME_CURRENT
   ```
   (de verificat pe laptop: aliasul `LIDACTION`)
   - De ce: altfel cineva închide capacul la plecare și robotul se oprește.
3. Laptopul stă permanent în priză. Robotul nu se bazează pe baterie.
   - De ce: setările de mai sus se aplică doar pe alimentare.
4. Windows Update: Settings > Windows Update > Advanced options > **Active hours** = intervalul în care lucrează robotul (de verificat: limita maximă a intervalului).
   - De ce: Windows repornește pentru actualizări doar în afara orelor active; o repornire în timpul unui job lasă itemul `InProgress` până devine `Abandoned`.
   - După fiecare actualizare mare: verifică faptul că robotul apare conectat (§7).
5. Pentru contul `robot-pixeldata` (logat ca robot): pornește **Do not disturb** (Windows 11) sau **Focus assist** = Alarms only (Windows 10) (de verificat: calea exactă).
   - De ce: o notificare care acoperă un buton din PixelData face robotul să nu-l găsească (`PIXELDATA_UI_TIMEOUT`).

## 4. Afișaj: aceeași rezoluție și scalare

1. Alege o singură configurație, de ex. **1920×1080** și scalare **100%**.
   - De ce: selectoarele, Computer Vision și click-urile în grila de ore din PixelData depind de poziții și pixeli. O altă rezoluție sau scalare le strică.
2. Setează aceeași rezoluție și scalare în ambele conturi Windows (Settings > System > Display):
   - contul tău de dezvoltare, în care capturezi ecranele PixelData în Studio ([08-construire-in-studio.md](08-construire-in-studio.md))
   - contul `robot-pixeldata`
   - Scalarea se salvează per utilizator (de verificat), deci setează-o în fiecare cont.
   - De ce: ce ai capturat ca dezvoltator trebuie să arate identic pentru robot.
3. Pe Pro (RDP mode), rezoluția sesiunii robotului vine din Orchestrator, nu din Windows. Setează aceleași valori în §8.
   - De ce: o sesiune RDP poate porni cu altă rezoluție decât ecranul fizic.
4. Monitor extern: **nu** conecta și deconecta un monitor extern după ce ai capturat ecranele.
   - Un monitor extern schimbă rezoluția și scalarea pentru toate conturile.
   - Recomandat: laptopul lucrează mereu fără monitor extern. Dacă ai nevoie de monitor, folosește-l mereu pe același, și la captură, și la rulare.
   - De ce: e cea mai frecventă cauză pentru „a mers ieri, azi nu mai găsește elementul”.

## 5. Politici de grup (GPO) pentru erorile cunoscute

1. Pornește `gpedit.msc` ca administrator (`Win+R` > `gpedit.msc`).
   - Pe **Home**, `gpedit.msc` poate lipsi (de verificat). Nu instala pachete neoficiale care îl adaugă. Variantele: cheile de registru echivalente (de verificat) sau trecerea la Pro.
   - De ce: fără aceste politici apar două erori cunoscute care opresc joburile unattended.
2. Pentru eroarea „Cannot bring the target application in foreground because the Windows session is locked”, pune pe **Disabled**:

| Politică | Unde (de verificat: calea exactă) |
|---|---|
| „Enable news and interests on the taskbar” | Computer Configuration > Administrative Templates > Windows Components > News and interests. Pe Windows 11, echivalentul poate fi „Allow widgets”, sub Widgets (de verificat) |
| „Set time limit for active but idle Remote Desktop Services sessions” | Computer Configuration > Administrative Templates > Windows Components > Remote Desktop Services > Remote Desktop Session Host > Session Time Limits |
| „Sign-in and lock last interactive user automatically after a restart” | Computer Configuration > Administrative Templates > Windows Components > Windows Logon Options |

   - De ce: aceste setări blochează sau închid sesiunea robotului în timpul jobului.
3. Pentru eroarea „A specified logon session does not exist”, pune pe **Disabled**:

| Politică | Unde (de verificat: calea exactă) |
|---|---|
| „Display information about previous logons during user logon” | Computer Configuration > Administrative Templates > Windows Components > Windows Logon Options |

   - De ce: ecranul cu informații despre logările anterioare oprește deschiderea automată a sesiunii.
4. Aplică politicile: în Command Prompt ca administrator, `gpupdate /force`, apoi repornește laptopul.
   - De ce: politicile noi nu se aplică sesiunilor deja deschise.

## 6. Instalare Studio + Unattended Robot

1. Descarcă instalatorul din **Resource Center** în Automation Cloud (de verificat: unde e linkul în meniu).
   - Versiunea: **2025.10 LTS** (la 2026-09-10: 2025.10.17).
   - Tipul: MSI **Enterprise** (contul e trial Enterprise), nu Community (de verificat: numele exact al fișierului). Licența Community e doar pentru uz necomercial ([09-licente-gdpr-riscuri.md](09-licente-gdpr-riscuri.md)).
   - De ce: LTS primește corecturi mai mult timp, iar Studio alege singur versiunile compatibile ale pachetelor de activități.
2. Rulează MSI-ul ca administrator > alege **Custom** (nu Quick).
   - De ce nu Quick install: Quick instalează în „user mode”, adică doar robot Attended, care rulează numai când ești tu logat. Robotul Unattended trebuie să pornească singur și să deschidă sesiunea `robot-pixeldata` când nu e nimeni la laptop.
3. În Custom:
   - **Install for all users** (instalare per mașină)
   - componente: **Studio** + **Unattended Robot**
   - calea implicită (de verificat: `C:\Program Files\UiPath\Studio\`)
   - De ce: „all users” instalează robotul ca serviciu Windows (service mode), care pornește odată cu laptopul.
4. Verifică serviciul robotului, în PowerShell:
   ```powershell
   Get-Service UiRobotSvc
   ```
   - Status așteptat: `Running` (de verificat: numele serviciului).
   - De ce: fără serviciu pornit, Orchestrator nu poate trimite joburi.
5. Deschide Studio din contul tău de dezvoltare și loghează-te cu contul tău UiPath (licența din [06-setup-orchestrator.md](06-setup-orchestrator.md) §0).
   - Nu folosi Studio din contul `robot-pixeldata`.
   - De ce: contul robotului e doar pentru joburi; dezvoltarea în sesiunea lui intră în conflict cu joburile.

## 7. Conectarea robotului la Orchestrator

Condiții: în Orchestrator există machine template-ul `PixelData-Laptop` (Client ID + Client Secret) și robot account-ul `robot-pixeldata` ([06-setup-orchestrator.md](06-setup-orchestrator.md) §4–§5).

1. PowerShell ca administrator. Verifică întâi parametrii acceptați:
   ```powershell
   & "C:\Program Files\UiPath\Studio\UiRobot.exe" --help
   ```
   (de verificat: calea `UiRobot.exe`)
   - De ce: numele parametrilor pot diferi între versiuni.
2. Conectează robotul:
   ```powershell
   & "C:\Program Files\UiPath\Studio\UiRobot.exe" connect --url "https://cloud.uipath.com/{org}/{tenant}/orchestrator_" --clientID "<client-id>" --clientSecret '<client-secret>'
   ```
   - Pune secretul între apostrofuri simple. În PowerShell, `$` din ghilimele duble e tratat ca variabilă și secretul ajunge trunchiat.
   - De ce: legătura cu machine template-ul spune Orchestrator că acest laptop poate rula joburile din folderul `PixelData`.
3. Șterge secretul din istoricul PowerShell:
   ```powershell
   Remove-Item (Get-PSReadLineOption).HistorySavePath
   ```
   - Comanda șterge tot istoricul salvat. Alternativa fără istoric: UiPath Assistant > Preferences > Orchestrator Settings, conectare cu Client ID (de verificat: etichetele).
   - De ce: istoricul e un fișier text pe disc; oricine îl citește poate conecta alt calculator ca robot.
4. Verifică legătura:
   - UiPath Assistant (iconița din bara de sistem) arată robotul conectat și licențiat (de verificat: textul exact).
   - Orchestrator > Tenant > **Machines** (sau pagina de monitorizare a mașinilor, de verificat): `PixelData-Laptop` apare cu `<NUME-LAPTOP>` și starea conectat.
   - De ce: dacă robotul nu apare conectat, orice job din trigger rămâne `Pending`.

## 8. Setările robotului în Orchestrator

1. Orchestrator > robot account-ul `robot-pixeldata` > Edit > setările robotului (de verificat: dacă setările sunt pe robot account sau pe machine template).
2. Completează:

| Setare | Pro (RDP mode) | Home (console mode) | De ce |
|---|---|---|---|
| `Login To Console` | `No` | `Yes` | Pe Pro, robotul are sesiunea lui RDP; Home nu poate fi gazdă RDP |
| `Resolution Width` | `1920` | `1920` | Aceeași lățime ca la captură (§4) |
| `Resolution Height` | `1080` | `1080` | Aceeași înălțime ca la captură (§4) |
| `Resolution Depth` | `32` | `32` | Adâncime de culoare uzuală (de verificat: valoarea implicită) |

   - Valorile de rezoluție sunt exemplul din §4. Folosește valorile alese de tine acolo.
   - Pe Home nu e confirmat că setările de rezoluție se aplică în console mode (de verificat). Rezoluția ecranului fizic trebuie oricum să fie cea din §4.
3. Pe **Home**: sesiunea robotului apare pe ecranul fizic. Ține laptopul într-o cameră închisă, nu la ghișeu.
   - De ce: pe ecran se văd datele pacienților ([09-licente-gdpr-riscuri.md](09-licente-gdpr-riscuri.md)).
4. Salvează, apoi rulează smoke test-ul din [06-setup-orchestrator.md](06-setup-orchestrator.md) §9.

## 9. Reguli zilnice pentru oameni

| Regulă | De ce |
|---|---|
| La plecarea de la laptop: **Sign out** (Start > cont > Sign out). **Nu** Lock (`Win+L`) | Windows desktop are o singură sesiune activă. O sesiune umană blocată împiedică sesiunea robotului („session is locked”) |
| Nu depana în Studio cât rulează un job | Studio și robotul folosesc același PixelData și aceeași mașină; Studio poate lua focusul sau fereastra robotului |
| Nu folosi userul PixelData al robotului și nu deschide PixelData manual în contul `robot-pixeldata` | O a doua logare poate închide sesiunea robotului |
| Nu conecta un monitor extern și nu schimba rezoluția sau scalarea | Robotul nu mai găsește elementele (§4) |
| Nu schimba parola Windows a robotului fără să o actualizezi în Orchestrator | Joburile nu mai pornesc (§11) |
| Nu închide capacul și nu scoate laptopul din priză dacă §3 nu e aplicat | Laptopul adoarme și robotul se deconectează |

Ce se întâmplă când laptopul e oprit:
- Itemii noi stau `New` în coadă, fără expirare.
- Joburile rămân `Pending` până revine robotul.
- La pornire, robotul se reconectează, iar triggerul reverifică coada în cel mult 30 de minute.
- O programare a cărei oră a trecut între timp nu se mai introduce: robotul oprește itemul cu `APPOINTMENT_IN_PAST`. Un om o introduce manual, dacă mai e cazul.

Ce se întâmplă dacă laptopul cade în mijlocul unui job (pană de curent, blocare):
- La reconectare, robotul repornește jobul, dar itemul rămas `InProgress` nu este reluat automat.
- După 24 de ore, itemul devine `Abandoned`; auto retry îl preia din nou.
- Între timp, PixelData poate avea deja programarea salvată chiar înainte de cădere.
- De aceea, robotul verifică întâi „există deja programarea?” (CNP + dată + oră + resursă). Dacă da, marchează itemul `Successful` cu `Outcome` = `already_existed` și nu creează un duplicat.
- După o cădere, un om verifică în PixelData dacă a rămas o fișă completată pe jumătate (de verificat: ce face PixelData cu o fișă nesalvată).

## 10. Securitatea laptopului (date ale pacienților pe disc)

1. Pornește **BitLocker** pe discul sistemului (Control Panel > BitLocker Drive Encryption, de verificat). Pe Home există doar „Device encryption”, și numai pe hardware compatibil (de verificat).
   - Salvează cheia de recuperare în password manager, nu pe laptop.
   - De ce: un laptop furat fără criptare înseamnă capturi de ecran și loguri cu date ale pacienților, lizibile de oricine.
2. Fișiere care pot conține date ale pacienților:
   - `Exceptions_Screenshots`: capturi de ecran făcute de REFramework la excepții. Stau în folderul de execuție al procesului, în profilul `robot-pixeldata` (de verificat: calea exactă pe robot).
   - Logurile locale UiPath: `%LocalAppData%\UiPath\Logs` în profilul `robot-pixeldata` (de verificat).
   - De ce: pe ecranele PixelData și în mesajele de log pot apărea nume, CNP, telefon.
3. Reguli pentru aceste fișiere:
   - nu intră niciodată în git (`.gitignore` ignoră deja `Exceptions_Screenshots/` și `*.log`)
   - nu se copiază pe stick USB și nu se trimit pe email sau chat
   - acces doar pentru `robot-pixeldata` și administratori (click dreapta pe folder > Properties > Security)
   - în workflow nu se loghează date ale pacienților ([08-construire-in-studio.md](08-construire-in-studio.md))
4. Curățenie periodică: șterge fișierele mai vechi de 30 de zile. Exemplu, rulat în contul `robot-pixeldata` (sau ca Scheduled Task pe acel cont):
   ```powershell
   Get-ChildItem "$env:LOCALAPPDATA\UiPath\Logs" -Recurse -File |
     Where-Object LastWriteTime -lt (Get-Date).AddDays(-30) |
     Remove-Item
   ```
   - Aplică la fel pe folderul `Exceptions_Screenshots`.
   - 30 de zile e o propunere; perioada reală de păstrare se stabilește în [09-licente-gdpr-riscuri.md](09-licente-gdpr-riscuri.md) (de verificat).
   - De ce: mai puține fișiere vechi = mai puține date expuse dacă laptopul e compromis.
5. Contul `robot-pixeldata` rămâne Standard user, fără alte programe instalate și fără navigare personală.
   - De ce: fiecare program în plus e o cale de acces la sesiunea care vede datele pacienților.

## 11. Depanare

| Eroare sau simptom | Cauză | Rezolvare |
|---|---|---|
| „Cannot bring the target application in foreground because the Windows session is locked” | Sesiune blocată: un om a apăsat `Win+L`, sau politicile din §5.2 sunt încă active | Omul face Sign out. Aplică §5.2, `gpupdate /force`, repornește laptopul |
| „A specified logon session does not exist” | Politica „Display information about previous logons during user logon” e activă | Aplică §5.3, `gpupdate /force`, repornește laptopul |
| Elementul nu e găsit / `PIXELDATA_UI_TIMEOUT` imediat după ce s-a schimbat monitorul, rezoluția sau scalarea (de verificat: textul exact al erorii) | Rezoluția sau scalarea diferă de cea de la captură | Revino la configurația din §4. Verifică `Resolution Width/Height` în §8. Recaptură doar dacă schimbarea e definitivă |
| Jobul rămâne `Pending`, deși laptopul e pornit | Un om e logat sau are sesiunea blocată; robotul nu poate deschide sesiunea lui | Omul face Sign out. Pe Home, nimeni nu rămâne logat pe ecran |
| Jobul pică la pornire cu o eroare de logare, de ex. user sau parolă greșite (de verificat: textul exact) | Parola Windows a `robot-pixeldata` a expirat sau a fost schimbată | Ca administrator: `net user robot-pixeldata *` (parolă nouă). Aplică §1.3. Actualizează parola în robot account ([06-setup-orchestrator.md](06-setup-orchestrator.md) §5) |
| Itemii eșuează cu `PIXELDATA_LOGIN_FAILED` | Parola PixelData a expirat sau s-a schimbat, ori userul robotului e logat în altă parte | Actualizează assetul `PixelData_RobotLogin` ([06-setup-orchestrator.md](06-setup-orchestrator.md) §3). Întreabă producătorul despre logări concurente (§2) |
| Mașina apare deconectată în Orchestrator | Laptop adormit, fără internet, serviciul robotului oprit, sau secretul mașinii regenerat | Verifică §3. `Get-Service UiRobotSvc`. Reconectează cu §7 |

## 12. Checklist final

- [ ] Ediția Windows notată: Pro (RDP mode) sau Home (console mode)
- [ ] Rezoluția și scalarea alese și notate (de ex. 1920×1080, 100%)
- [ ] Contul local `robot-pixeldata`: parolă, Standard user, parola nu expiră
- [ ] Pe Pro: `robot-pixeldata` în grupul Remote Desktop Users
- [ ] Prima logare manuală ca `robot-pixeldata` făcută, apoi Sign out
- [ ] Userul PixelData dedicat robotului există; întrebările despre logări concurente trimise producătorului
- [ ] Sleep pe alimentare = Never; capac închis pe alimentare = Do nothing; laptopul stă în priză
- [ ] Active hours pentru Windows Update setate
- [ ] Do not disturb / Focus assist pornit pentru `robot-pixeldata`
- [ ] Aceeași rezoluție și scalare în contul de dezvoltare și în `robot-pixeldata`; fără monitor extern
- [ ] Cele 4 politici din §5 pe Disabled, `gpupdate /force`, laptop repornit
- [ ] Studio + Unattended Robot 2025.10 LTS instalate cu Custom > Install for all users
- [ ] Serviciul robotului rulează
- [ ] Robotul conectat la Orchestrator; secretul șters din istoricul PowerShell
- [ ] În Orchestrator: `Login To Console` și `Resolution Width/Height/Depth` setate
- [ ] BitLocker (sau Device encryption) pornit; cheia de recuperare în password manager
- [ ] Curățenia pentru `Exceptions_Screenshots` și loguri stabilită
- [ ] Oamenii care folosesc laptopul cunosc regulile din §9
- [ ] Toate „(de verificat)” din acest document confirmate sau corectate
