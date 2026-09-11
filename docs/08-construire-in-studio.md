# 08. Construirea procesului în Studio

Cum se construiește procesul `PixelDataProgramari`, pe laptopul Windows, de la proiect gol la proces publicat în Orchestrator. Scris pentru cineva care deschide Studio pentru prima dată.

Ordinea pe laptop: [07](07-setup-laptop.md) → [06](06-setup-orchestrator.md) → **08** → [10](10-plan-teste.md).

Ce se construiește, workflow cu workflow: [`robot/WORKFLOWS.md`](../robot/WORKFLOWS.md). Acest document spune cum se face proiectul în jurul lor.

Nimic din ce urmează nu a fost rulat: PixelData și Studio nu există pe Linux. Pașii marcați „(de verificat)” se confirmă la prima rulare pe laptop.

## 0. Înainte de a începe

| Condiție | Unde |
|---|---|
| Laptopul pregătit: cont `robot-pixeldata`, alimentare, GPO, rezoluție fixată, Studio + Unattended Robot instalate, robot conectat | [07](07-setup-laptop.md) §1–§8 |
| Orchestrator: folderul `PixelData`, coada `PixelData_Programari`, assetul `PixelData_RobotLogin`, machine template, robot account | [06](06-setup-orchestrator.md) §1–§5 |
| Repo-ul clonat pe laptop (ai nevoie de `robot/Logic`, `robot/Data`, `robot/Config.rows.csv`, `tools/queue-client`) | — |
| PixelData instalat, cu userul dedicat robotului | [07](07-setup-laptop.md) §2 |
| Un pacient de test și o resursă de test, stabilite cu clinica | [10](10-plan-teste.md) §8 |

Lucrează în **contul tău de dezvoltare**, nu în contul `robot-pixeldata` ([07](07-setup-laptop.md) §6).

## 1. Proiectul REFramework

1. Studio > **New Project** > **Templates** > **Robotic Enterprise Framework**.
2. Completează:

   | Câmp | Valoare |
   |---|---|
   | Name | `PixelDataProgramari` |
   | Location | un folder al tău, în afara repo-ului clonat (de exemplu `C:\UiPath\PixelDataProgramari`) |
   | Compatibility | **Windows** |
   | Language | **C#** |

   - **Windows**, nu „Windows - Legacy” și nu „Cross-platform”: automatizarea aplicațiilor desktop merge doar pe Windows (investigatie §4).
   - **C#**: toate expresiile din acest proiect și din [`robot/WORKFLOWS.md`](../robot/WORKFLOWS.md) sunt C#. Limbajul se alege acum și nu se schimbă ușor după.
   - Locația: proiectul NU se pune în repo. Repo-ul ține sursele (`robot/`), proiectul Studio e generat pe laptop (decizia D9 din [01](01-arhitectura.md)).
3. Lasă Studio să descarce pachetele de activități. Versiunile le alege singur (Studio 2025.10 LTS).
4. Deschide `Main.xaml` și rulează-l o dată, ca să vezi că template-ul pornește. Va da eroare la coadă — normal, coada nu e încă în `Config.xlsx`.

Ce ai acum: `Main.xaml`, folderul `Framework` cu cele opt workflow-uri ale template-ului, `Config.xlsx`, `Data`, `Exceptions_Screenshots`, `Tests`.

## 2. Logica C#: `robot/Logic` în proiect

1. Creează folderul `Logic` în folderul proiectului.
2. Copiază **toate** fișierele `.cs` din `robot/Logic/` din repo în `PixelDataProgramari\Logic\`.
3. În Studio, panoul **Project** > butonul **Show All Files** — fișierele `.cs` apar în arbore.
4. **Ctrl+S** și apoi **Debug File** pe `Main.xaml`: dacă un `.cs` nu compilează, Studio raportează eroarea acum.

De ce așa:

- **Studio compilează automat orice fișier `.cs` din folderul proiectului.** Nu se adaugă nimic manual, nu există „Add reference”. De asta ajunge copierea.
- **De asta proiectul de teste stă în afara folderului proiectului UiPath.** `tests/Logic.Tests` include aceleași fișiere prin `<Compile Include="../../robot/Logic/**/*.cs" />`. Dacă ar fi copiat în proiect, Studio ar încerca să compileze și testele, împreună cu xunit, care nu e acolo (decizia D8 din [01](01-arhitectura.md)).
- Namespace-ul e `PixelDataProgramari.Logic`. În expresii îl poți scrie întreg (`PixelDataProgramari.Logic.QueueItemParser.FromSpecificContent(...)`) sau îl adaugi o dată în **Project Settings > Namespaces/Imports**, ca să scrii doar `QueueItemParser`.

**Sursa adevărului rămâne repo-ul.** O corectură se face în `robot/Logic/*.cs`, se rulează `dotnet test tests/Logic.Tests`, apoi se copiază din nou în proiect. Modificările făcute direct în proiect se pierd și nu sunt acoperite de teste.

Rulează testele pe laptop cel puțin o dată, înainte de a construi XAML-ul:

```powershell
dotnet test tests\Logic.Tests
```

Detalii: [10](10-plan-teste.md) §1.

## 3. Fișierul de mapări

1. Copiază `robot/Data/pixeldata-mappings.json` în `PixelDataProgramari\Data\`.
2. Deschide-l și completează valorile `"TODO"` **cu ce vezi în PixelData**: numele resursei din drop-down, valorile din `Sursa pacient:`, denumirile procedurilor.
3. Ce rămâne `"TODO"` se comportă ca valoare lipsă: robotul oprește itemul cu `RESOURCE_MAPPING_MISSING` sau `PATIENT_SOURCE_MAPPING_MISSING`.

Pentru primele teste ajunge **un singur rând** completat: sucursala și resursa de test. Restul se completează pe măsură ce clinica confirmă valorile ([04](04-mapare-campuri.md) §3).

Fișierul se citește o singură dată, în `InitAllApplications.xaml` ([`robot/WORKFLOWS.md`](../robot/WORKFLOWS.md) §4.1). O modificare cere repornirea jobului.

Corecturile se duc înapoi în repo, în `robot/Data/pixeldata-mappings.json` — dar **fără nume reale de clinici sau de medici** dacă acestea sunt date personale; discută cu clinica ce poate intra în repo ([09](09-licente-gdpr-riscuri.md)).

## 4. `Config.xlsx`

`robot/Config.rows.csv` din repo are un rând pentru fiecare valoare de pus în `Config.xlsx`: foaia (`Sheet`), numele, valoarea și explicația.

1. Deschide `PixelDataProgramari\Data\Config.xlsx` în Excel.
2. Pentru rândurile marcate „Rând existent în template” (`OrchestratorQueueName`, `OrchestratorQueueFolder`, `logF_BusinessProcessName`): **schimbă doar valoarea**, nu adăuga rânduri noi.
3. Pentru rândurile marcate „Rând nou”: adaugă-le la finalul foii indicate (`Settings` sau `Constants`), cu numele exact din CSV.
4. `PixelDataExePath`: pune calea completă a executabilului PixelData de pe laptop. Din ea se deduce și numele procesului, folosit de `KillAllProcesses.xaml`.
5. `MaxRetryNumber` rămâne `0`. Retry-ul îl face coada (`Auto retry`, maximum 1). Orice valoare peste 0 ar adăuga un al doilea retry, în Studio, peste cel al cozii, iar un item ar fi procesat de mai multe ori.
6. Salvează și închide Excel înainte de a rula: un fișier deschis poate bloca citirea (de verificat).

**Credential asset-ul nu se pune în foaia `Assets`.** `InitAllSettings.xaml` citește foaia `Assets` cu **Get Asset**, care nu întoarce assets de tip Credential. Numele assetului stă în `Settings`, ca text (`PixelDataCredentialAsset`), și se citește cu **Get Credential** în `InitAllApplications.xaml` (de verificat comportamentul exact al `Get Asset` pe un Credential).

Parola nu ajunge niciodată în `Config`, în log sau într-o variabilă `String`: rămâne `SecureString` și se folosește cu **Type Secure Text**.

## 5. Captura interfeței PixelData în Object Repository

Object Repository ține elementele de interfață într-un singur loc, ca să nu ai același selector copiat în zece activități. Structura: **App > Version > Screens > Elements**.

1. Deschide PixelData și logează-te manual, ca să ajungi la ecranele pe care le capturezi.
2. În Studio: panoul **Object Repository** > **Create Application** > numele `PixelData` > o versiune (de verificat versiunea reală a aplicației).
3. Pentru fiecare ecran din [`robot/WORKFLOWS.md`](../robot/WORKFLOWS.md) §7 (`Login`, `Principal`, `Programari`, `Fisa`, `DateDemografice`, `Popups`): **Capture Screen**, apoi capturează elementele listate acolo, cu numele de acolo.
4. Pentru fiecare element, verifică-l cu **Validate** și, dacă e instabil, adaugă o **ancoră** (o etichetă fixă de lângă el).
5. Dacă un selector nu prinde nimic, schimbă framework-ul în **UI Explorer**: `Default` → `Active Accessibility` → `UI Automation`. Controalele Delphi răspund uneori doar la unul dintre ele (de verificat, control cu control).

Ordinea de țintire, de la cel mai stabil la cel mai fragil:

| Ordine | Metodă | Când |
|---|---|---|
| 1 | selector Strict sau Fuzzy, cu ancoră | implicit, peste tot |
| 2 | Computer Vision | celulele grilei de ore, dacă nu sunt expuse ca elemente |
| 3 | Image | ultima variantă; se strică la orice schimbare de temă sau rezoluție |

Două elemente au **selector parametrizat**, fiindcă se schimbă la fiecare rulare: ziua din mini-calendar și rândul unei ore din grilă. Se capturează o dată, apoi în selector se înlocuiește textul fix cu un argument (de verificat mecanismul exact în versiunea instalată).

Cele două ferestre `Atenție` (starea lipsă și golirea câmpurilor) au **același titlu**. Selectorul lor trebuie să conțină textul din fereastră, altfel robotul le confundă și apasă butonul greșit.

## 6. Metoda de input

Fiecare `Click` și `Type Into` are o proprietate **Input Method**:

| Metodă | Ce face | Când |
|---|---|---|
| `Simulate` | trimite mesajul direct controlului; merge în fundal, nu mișcă mouse-ul, e cea mai rapidă și cea mai sigură | implicit, se încearcă prima |
| `SendWindowMessages` | trimite mesaje Windows ferestrei | când `Simulate` nu are efect |
| `HardwareEvents` | mișcă mouse-ul și apasă tastele fizic; cere ecranul deblocat și fereastra în față | ultima variantă |

**Controalele Delphi pot să nu răspundă la `Simulate` și pot cere `HardwareEvents` (de verificat, control cu control).** Testează în ordinea de mai sus: dacă un `Type Into` cu `Simulate` „reușește” dar câmpul rămâne gol, treci la următoarea metodă. Verifică întotdeauna cu **Get Text** că valoarea a intrat.

`HardwareEvents` schimbă regulile de operare: jobul are nevoie de o sesiune deblocată și de fereastra în prim-plan, deci nimeni nu trebuie să lucreze pe laptop în timpul rulării ([07](07-setup-laptop.md) §9).

## 7. Rezoluție și DPI

Rezoluția și scalarea trebuie să fie **identice** în contul în care capturezi ecranele și în sesiunea robotului. O captură făcută la altă scalare trimite click-urile în altă parte, iar Computer Vision și grila de ore nu mai găsesc nimic.

Totul e descris în [07](07-setup-laptop.md) §4, inclusiv setarea rezoluției sesiunii robotului din Orchestrator (§8 acolo) și regula despre monitorul extern. Verifică-le înainte de prima captură — o captură făcută greșit se reface toată.

## 8. Construirea workflow-urilor

[`robot/WORKFLOWS.md`](../robot/WORKFLOWS.md) e specificația: fiecare workflow cu calea, argumentele, pașii, elementele și codul de eroare. Ordinea de construcție e în §8 acolo, și e aleasă ca fiecare pas să fie testabil singur.

Reguli de lucru:

1. **Un workflow pe rând.** Îl construiești, îi pui valori implicite pe argumentele de intrare, îl rulezi cu **Debug File** și vezi fiecare pas. Abia apoi treci la următorul.
2. **Fiecare workflow care atinge interfața are propriul Use Application/Browser** pe fereastra principală, cu `Open = Never` și `Close = Never`. Aplicația se deschide o singură dată, în `Login.xaml`, și se închide o singură dată, în `CloseAllApplications.xaml`.
3. **Conversia excepției de business se face într-un singur loc**, în `Process.xaml`:

   ```
   catch BusinessRuleViolation ex  ⇒  throw new BusinessRuleException(ex.Message)
   ```

   `Main.xaml` recunoaște doar `BusinessRuleException`. O `BusinessRuleViolation` ajunsă până acolo ar fi tratată ca eroare de sistem, iar coada ar reîncerca itemul cu exact aceleași date greșite.
4. **Erorile de sistem** se aruncă cu `throw new ApplicationException("<COD>: <mesaj>")`. Orice excepție care nu e `BusinessRuleException` e tratată de REFramework ca eroare de sistem.
5. **Mesajele nu conțin date ale pacientului.** Ajung în `ProcessingException`, vizibil în Orchestrator ([09](09-licente-gdpr-riscuri.md)).
6. **Modificările în `Main.xaml`** sunt exact două, descrise în [`robot/WORKFLOWS.md`](../robot/WORKFLOWS.md) §3. Restul template-ului rămâne neatins.

Pașii 2–4 din ordinea de construcție creează programări reale în PixelData. Folosește doar pacientul de test și resursa de test, și șterge-le la final ([10](10-plan-teste.md) §5, §8).

## 9. Ce ajunge de fapt în `SpecificContent` (de verificat pe laptop)

`QueueItemParser` primește `in_TransactionItem.SpecificContent`, adică un dicționar `cheie → Object`. **Tipul valorilor nu e garantat.** Robotul deserializează itemul cu Newtonsoft, deci o valoare poate veni ca `String`, `Boolean`, `Int64`, `Double`, `DateTime` sau ca un `JValue` (un obiect Newtonsoft care se comportă ca valoarea din el). Un text ISO-8601 cu oră poate ajunge **deja transformat în `DateTime`** — iar `ToString()` pe el depinde de cultura Windows și pierde offset-ul.

Parserul e scris ca să nu depindă de asta. Face două treceri.

### Pasul 1 — normalizarea (`SpecificContentValues.Normalize`)

Aduce orice valoare la un tip cunoscut:

| Ce vine | Ce devine |
|---|---|
| `null`, `DBNull` sau tip gol | `null` ⇒ `INVALID_FIELD` |
| `DateTimeOffset` | rămâne `DateTimeOffset` |
| obiect sau listă JSON | marcat ca valoare structurată ⇒ `INVALID_FIELD` (contractul are doar chei plate) |
| orice `IConvertible`, inclusiv `JValue` | se citește după `GetTypeCode()`: `Boolean` → `bool`; `String`/`Char` → text; `DateTime` → `DateTime`; întregi până la `Int64` → `long`; `UInt64` și `Decimal` → `decimal`; `Single` și `Double` → `double` |
| alt `IFormattable` | `ToString(null, CultureInfo.InvariantCulture)` |
| orice altceva | `ToString()` |

Toate conversiile folosesc `CultureInfo.InvariantCulture`. Asta e miezul: un `JValue` care conține un număr sau o dată ar da, prin `ToString()` simplu, un text formatat după cultura mașinii — virgulă zecimală, `dd.MM.yyyy` — și parserul l-ar refuza pe un laptop românesc, deși ar merge pe unul englezesc.

### Pasul 2 — citirea pe tipul câmpului

| Tip câmp | Chei | Ce acceptă |
|---|---|---|
| text | majoritatea | text ca atare, **fără `Trim`**; `bool` devine `"true"`/`"false"`; numerele în formă invariantă; o dată devine text ISO-8601 |
| dată și oră | `CreatedAt`, `ScheduledAt` | `DateTimeOffset`; `DateTime` cu `Kind` `Utc` sau `Local`; text RFC3339 **cu offset** (`Z` sau `±HH:MM`), fracțiunea de secundă trunchiată la 7 cifre. **`DateTime` cu `Kind` `Unspecified` ⇒ `INVALID_FIELD`**, fiindcă nu are offset |
| doar dată | `ScheduledLocalDate`, `ReferralDate`, `PatientBirthDate` | `DateTime` sau `DateTimeOffset` (se ia partea de dată); text `AAAA-LL-ZZ`. Text gol = necunoscut doar la `ReferralDate` și `PatientBirthDate` |
| oră | `ScheduledLocalTime` | **doar text `HH:MM`**. Un `DateTime` sau un `TimeSpan` pe această cheie dă `INVALID_FIELD` |
| întreg | `DurationMinutes` | `long`, sau `double`/`decimal` fără zecimale, sau text format doar din cifre; negativ sau peste `Int32.MaxValue` ⇒ `INVALID_FIELD` |
| boolean | `ReferralPending` | `bool`, sau textul `"true"`/`"false"` (majusculele nu contează) |

Peste asta se verifică formatele: UUID `8-4-4-4-12` la `AppointmentId` și `PatientId` (și la `BranchId`, `ProductId` când nu sunt goale); listele închise la `Source`, `Laterality`, `Payer`; E.164 la `PatientPhone` (`+` urmat de 2–15 cifre, prima ne-zero); maximum 2000 de caractere la `Notes`. `PatientCnp` nu e verificat de parser: `AppointmentValidator` dă `CNP_REQUIRED` sau `CNP_INVALID`.

O cheie lipsă dă `MISSING_FIELD`, o cheie necunoscută e ignorată.

### Ce se verifică la prima rulare

Două lucruri decid dacă parserul merge sau refuză toți itemii, și niciunul nu poate fi verificat pe Linux:

1. **Ce tip primește `ScheduledAt`.** Pune un **Log Message** la începutul lui `Process.xaml`:

   ```
   in_TransactionItem.SpecificContent["ScheduledAt"].GetType().FullName
   ```

   - `System.String` ⇒ textul RFC3339 ajunge ca text, totul e în regulă.
   - un `DateTime` cu `Kind` `Utc` sau `Local` ⇒ tot în regulă, parserul îl convertește.
   - un `DateTime` cu `Kind` `Unspecified` ⇒ **fiecare item ar eșua cu `INVALID_FIELD`**. Atunci se decide, cu echipa, fie transformarea în `Process.xaml` înainte de parsare, fie relaxarea regulii în `robot/Logic` — niciodată o soluție locală într-un XAML.
2. **Ce tip primește `ScheduledLocalTime`.** Cheia acceptă doar text `HH:MM`. Dacă Robot-ul o transformă în `DateTime` sau `TimeSpan`, e aceeași discuție ca mai sus.

Verifică amândouă la **primul** item pus în coadă, nu la testul end-to-end: sunt eșecuri care arată ca „date greșite” dar nu au nimic de-a face cu datele.

## 10. Depanarea cu un item real

Nu inventa un `QueueItem` în Studio: ia unul adevărat din coadă, exact ca la rulare.

1. Pune un item în coada `PixelData_Programari` de pe calculatorul de dezvoltare, cu `tools/queue-client`. Comenzile exacte: [`tools/queue-client/README.md`](../tools/queue-client/README.md); pașii compleți, cu ce trebuie verificat în exemplu înainte de trimitere: [06](06-setup-orchestrator.md) §9.
2. Verifică în Studio că proiectul e conectat la Orchestrator, la folderul `PixelData` (altfel `GetTransactionData.xaml` nu găsește coada).
3. **Oprește triggerul** `PixelData_Programari_OnNewItem` sau asigură-te că robotul unattended nu rulează — altfel jobul unattended ia itemul înaintea ta.
4. **Debug** pe `Main.xaml`, cu **Highlight Elements** pornit, ca să vezi ce atinge robotul.
5. Când ceva se oprește, folosește **Locals** și **Immediate** ca să vezi valorile; `Exceptions_Screenshots` ține captura de la momentul excepției.
6. Nu depana în Studio cât rulează un job unattended pe același laptop: se bat pe aceeași sesiune Windows și pe aceeași instanță PixelData.

Un item consumat în debug nu mai e `New`. Pentru încă o rulare trimite alt item, cu alt `AppointmentId` — coada refuză același `Reference`.

## 11. Publicarea în Orchestrator

1. Studio > **Publish** > destinație **Orchestrator Personal Workspace** sau **Orchestrator Tenant/Folder** — folderul `PixelData`.
2. Versiune: lasă Studio să o incrementeze; scrie o notă scurtă cu ce s-a schimbat.
3. În Orchestrator, procesul `PixelDataProgramari` din folderul `PixelData` trece pe versiunea nouă ([06](06-setup-orchestrator.md) §6).
4. Rulează smoke test-ul din [06](06-setup-orchestrator.md) §9: un item pus în coadă, jobul pornit de trigger, programarea vizibilă în PixelData.

După fiecare publicare, testul de duplicat din §9 acolo: al doilea item cu același `AppointmentId` trebuie respins de coadă, fără job nou.

## 12. Checklist

- [ ] Proiect `PixelDataProgramari`, REFramework, Windows, C#, creat în afara repo-ului.
- [ ] `robot/Logic/*.cs` copiate în `Logic\`; `Main.xaml` compilează.
- [ ] `dotnet test tests\Logic.Tests` trece pe laptop.
- [ ] `robot/Data/pixeldata-mappings.json` copiat în `Data\`, cu cel puțin resursa de test completată.
- [ ] `Config.xlsx` are toate rândurile din `robot/Config.rows.csv`; `MaxRetryNumber` = `0`; `PixelDataExePath` corect.
- [ ] Object Repository: ecranele și elementele din [`robot/WORKFLOWS.md`](../robot/WORKFLOWS.md) §7, fiecare validat.
- [ ] Metoda de input stabilită pentru fiecare tip de control, cu verificare prin `Get Text`.
- [ ] Rezoluția și scalarea identice în contul de dezvoltare, în contul robotului și în setările din Orchestrator.
- [ ] Tipurile din `SpecificContent` verificate la primul item (§9).
- [ ] Fiecare workflow rulat singur cu Debug File ([10](10-plan-teste.md) §4).
- [ ] `Main.xaml` are cele două modificări din [`robot/WORKFLOWS.md`](../robot/WORKFLOWS.md) §3.
- [ ] Procesul publicat; smoke test-ul din [06](06-setup-orchestrator.md) §9 trecut.
- [ ] Programările și pacienții de test șterși din PixelData.
