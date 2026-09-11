# tools/queue-client

Client de linie de comandă, în Go, care pune o programare în coada UiPath Orchestrator `PixelData_Programari` și citește starea unui item.

Pentru ce există:
- acum: testul cap-coadă din [docs/06-setup-orchestrator.md](../../docs/06-setup-orchestrator.md) §9, fără Windows și fără Studio;
- mai târziu: forma de referință pentru dispecerul din recepția ScanExpert (faza 3, [docs/05](../../docs/05-integrare-receptie.md)), care face exact aceleași două apeluri.

Numai biblioteca standard Go. Fără dependențe externe, fără fișiere de stare: tokenul stă doar în memorie, atât cât ține o comandă.

## Cerințe

- Go 1.26 sau mai nou (`go version`).
- Aplicația externă `ScanExpert-Receptie-Dispatcher` cu scope `OR.Queues`, adăugată în folderul `PixelData` ([docs/06](../../docs/06-setup-orchestrator.md) §8).
- Pe Windows: WSL sau Git Bash (de verificat).

## Compilare

```bash
cd tools/queue-client
go build -o queue-client .
```

Binarul `queue-client` este ignorat de git.

## Configurare

```bash
cp .env.example .env
# completează .env
set -a; . ./.env; set +a
```

Cele 7 variabile sunt obligatorii, fără valori implicite. Una lipsă sau goală oprește comanda cu codul `2` și numește variabila. Tabelul cu valori și legătura cu Orchestrator: [docs/06](../../docs/06-setup-orchestrator.md) §11.

| Variabilă | Exemplu |
|---|---|
| `UIPATH_CLOUD_URL` | `https://cloud.uipath.com` (doar schema și hostul) |
| `UIPATH_ORG` | numele organizației din URL |
| `UIPATH_TENANT` | `DefaultTenant` |
| `UIPATH_CLIENT_ID` | App ID |
| `UIPATH_CLIENT_SECRET` | App Secret |
| `UIPATH_FOLDER_PATH` | `PixelData` |
| `UIPATH_QUEUE_NAME` | `PixelData_Programari` |

Scope-ul nu este variabilă: clientul cere mereu `OR.Queues`.

Valorile șablon din `.env.example` (`{org}`, `<client-secret>`) sunt refuzate ca atare, ca să nu pornească o comandă cu fișierul necompletat.

## Comenzi

### `enqueue`

```bash
./queue-client enqueue -file ../../contracts/examples/valid/cas-cu-bilet.json
```

Fișierul conține DOAR obiectul `SpecificContent` plat, niciodată plicul `{"itemData": ...}`; plicul îl construiește clientul ([contracts/README.md](../../contracts/README.md)). `Reference`-ul se derivă din `AppointmentId`: `create-<AppointmentId>`.

Afișează `Id`, `Reference` și `Status`.

Reference duplicat: clientul afișează `already queued: create-<AppointmentId>` și iese cu `0`. Coada are Enforce unique references, deci programarea e deja acolo și nu se adaugă un al doilea item.

Cum e recunoscut duplicatul:

- **Semnalul principal este codul de eroare documentat `1016` `DuplicateReference`**, mesaj „Error creating [ReferenceName]. Duplicate Reference.” ([response codes](https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/response-codes)). Se citește din câmpul `errorCode` al corpului JSON, **indiferent de codul HTTP**.
- **Codul HTTP nu declanșează nimic.** Codul HTTP cu care Orchestrator răspunde la un duplicat nu e documentat (de verificat) — o sursă de forum spune `409`. Dacă s-ar declanșa pe el, orice alt `409` ar fi citit ca duplicat, iar un duplicat răspuns cu alt cod ar fi ratat.
- **Rezervă: textul.** Când corpul nu e JSON sau nu are `errorCode`, clientul recunoaște orice răspuns ne-2xx al cărui corp conține „duplicate reference”, indiferent de majuscule. Rezerva rămâne ca un câmp lipsă să nu transforme un duplicat în eroare dură: altfel enqueue-ul ar fi raportat ca picat, deși programarea e deja în coadă.
- Un `errorCode` prezent și diferit de `1016` este alt eșec, oricare ar fi mesajul — codul bate textul. Clasificarea se face doar pentru `AddQueueItem`.

> **Forma corpului de eroare NU e documentată.** Pagina de response codes documentează *codurile* și înțelesul lor, dar nu arată niciun exemplu de JSON și nu numește niciodată un câmp `errorCode`. Citirea câmpului este deci comportament **observat**, nu contract (de verificat) — de aceea rezerva pe text rămâne și nimic nu e fatal când câmpul lipsește.

### Erori de configurare pe care le numește Orchestrator

Patru coduri documentate înseamnă că ce e greșit e configurarea, nu programarea. Toate ies cu `2`, nu cu `1`: un `1` invită la reîncercare, iar reîncercarea nu are cum să ajute. Mesajul numește variabila de schimbat, iar acel mesaj e scris în clientul nostru — nu conține niciun octet de la server, deci se poate afișa și fără `-debug`.

| Cod | Documentat ca | Ce afișează clientul |
|---|---|---|
| `1002` `ItemNotFound` | resursă inexistentă: „tenants, assets, jobs, host licenses, **queues and queue items**, processes, settings, and users” | verifică `UIPATH_QUEUE_NAME`; coada trebuie să existe în folderul din `UIPATH_FOLDER_PATH`. Codul acoperă și alte resurse, deci nu e dovadă că lipsește chiar coada |
| `1100` `InvalidOrganizationUnit` | apel făcut cu un user asociat altui organization unit decât cel cerut | verifică `UIPATH_FOLDER_PATH` |
| `1101` `RequiredOrganizationUnit` | POST fără organization unit ca parametru | headerul de folder n-a ajuns la Orchestrator, deși clientul îl trimite mereu — **defect în queue-client**, nu setare |
| `1850` `TransactionReferenceRequired` | numele e pe pagina de coduri, fără descriere; mesajul e pe [transactions-requests](https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/transactions-requests): „Error creating Transaction. Reference is required for Unique Reference Queues.” | coada cere `Reference` și cererea n-a avut unul, deși clientul îl trimite mereu — **defect în queue-client**, nu setare |

Un `403` care **nu** poartă vreun cod cunoscut iese tot cu `2`, cu mesaj care trimite la `UIPATH_FOLDER_PATH` și la permisiunile `Queues.View` / `Transactions.Create` din acel folder. **Maparea asta e dedusă, nu documentată**: nicio pagină oficială nu leagă un status HTTP de refuzul de permisiune pe acest endpoint.

Ce NU se face, ca să nu inventăm înțelesuri:

- Nu există cod documentat pentru „apelantul nu are `Queues.View` / `Transactions.Create` în folder”. Nu se fabrică unul.
- Documentația nu spune dacă un folder greșit răspunde *not-found* sau *not-authorised*. Clientul nu ramifică pe această distincție.
- `1017` `ForbiddenOperation` e documentat **doar** pentru descărcarea de pachete și librării, iar `1102` `OrganizationUnitNotEditable` doar pentru un endpoint de user. Niciunul nu se refolosește aici, oricât de generale le-ar suna numele.
- Un `errorCode` necunoscut rămâne `1`, fără mesaj de ajutor: nu se ghicește ce setare ar fi de vină.

### `status`

```bash
./queue-client status -reference create-232b0d5d-4321-465f-8533-e25bb5fba40b
```

Afișează `Id`, `Status`, `ProcessingException` (Type, Reason, Details) și `Output`. Mesajul excepției începe cu codul de eroare, de ex. `CNP_INVALID: <mesaj>`.

Retry-urile păstrează același `Reference`, deci contează itemul cu `Id`-ul cel mai mare — acela se afișează.

Un `Reference` pe care Orchestrator nu îl poate purta (gol, peste 128 de caractere, cu apostrof) este refuzat aici, cu codul `2`, fără să se trimită vreo cerere.

### `-debug`

Ambele comenzi acceptă `-debug`, care afișează în plus corpul răspunsului de eroare al Orchestrator. Este oprit implicit: un astfel de răspuns citează itemul înapoi, cu datele pacientului în el, iar mesajul obișnuit de eroare numește doar operația și codul HTTP. Așa, ce ajunge într-un log nu conține date de pacient. Pornește-l doar când chiar depanezi, într-un terminal la care ai tu acces.

## Coduri de ieșire

| Cod | Când |
|---|---|
| `0` | a mers (inclusiv un item deja în coadă cu același Reference) |
| `1` | Orchestrator a răspuns cu eroare sau a picat rețeaua |
| `2` | configurare, argumente sau fișier de intrare greșite — inclusiv coada sau folderul refuzate de Orchestrator (vezi tabelul de mai sus) |
| `3` | niciun item cu acel Reference (doar `status`) |

## Secrete

- Secretul aplicației și tokenul de acces nu ajung niciodată în ieșire, în erori sau în vreun `Config`/`Client` afișat. Există test pentru asta.
- Tokenul stă doar în memorie, nu se scrie pe disc.
- `.env` este ignorat de git. Dacă secretul ajunge pe un calculator necontrolat, rotește-l din Orchestrator.
- Mesajul unei erori de API numește doar operația și codul HTTP. Corpul răspunsului rămâne pe `APIError.Body` și se afișează doar cu `-debug`, pentru că poate conține date de pacient.
- `APIError.Hint` e altceva decât `Body`: e o constantă din pachetul `orchestrator`, nu poartă niciun octet trimis de server, deci se afișează mereu și se poate loga. Există test pentru asta.
- Tot ce vine dintr-un fișier sau de la server trece prin `orchestrator.CleanText` înainte de afișare: o secvență de escape într-un nume de pacient nu poate rescrie terminalul.
- Clientul nu urmează redirecturi. Un `3xx` devine eroare, ca să nu ajungă tokenul și itemul pe gazda numită de răspuns.
- Se citește doar dintr-un fișier obișnuit: un pipe cu nume ar citi cât timp scrie cineva în el.

## Pentru dispecerul din faza 3

Un `Client` se poate folosi din mai multe goroutine.

- Tokenul se cere o singură dată și se refolosește. Apelanții care pornesc fără token așteaptă aceeași cerere de token, iar apoi îl folosesc toți pe primul.
- Lacătul cache-ului nu se ține peste cererea HTTP, deci un apelant care are deja token nu așteaptă după cererea altuia.
- Un token cu viață scurtă este oricum păstrat cel puțin jumătate din durata lui, ca să nu ajungă fiecare apel o cerere nouă la serverul de identitate.
- `APIError.Body` NU se pune în log. Vezi mai sus de ce.
- Pool-ul de conexiuni este declarat explicit (`newHTTPClient` în `main.go`), nu moștenit: implicit sunt 2 conexiuni inactive per gazdă, iar un dispecer care lucrează la mai mult de doi itemi deodată ar reface TLS-ul pentru fiecare item în plus.
- Dispecerul primește **octeții** itemului, nu o cale de fișier. `readInput` are încredere deplină în calea primită: urmează symlink-uri și nu o închide într-un anumit director. Pentru un CLI rulat de operator cu drepturile lui e în regulă; pentru un serviciu care ar lua calea dintr-o cerere, nu.

## Structură

| Fișier | Ce este |
|---|---|
| `main.go` | comenzile `enqueue` și `status`, codurile de ieșire, afișarea |
| `orchestrator/config.go` | cele 7 variabile, validarea lor, URL-urile |
| `orchestrator/client.go` | tokenul (cerut o dată și păstrat), apelurile HTTP, erorile redactate |
| `orchestrator/queue.go` | `AddQueueItem`, citirea stării, `Reference`-ul, plicul `itemData` |

## Teste

```bash
cd tools/queue-client
go vet ./...
go test ./...
```

Testele pornesc un Orchestrator fals (`net/http/httptest`) și verifică cererea de token, refolosirea tokenului, plicul `itemData` cu headerele lui, duplicatul (după `errorCode` 1016 pe mai multe coduri HTTP, după text ca rezervă, și că alt `errorCode` nu e duplicat), erorile de configurare (fiecare din cele patru coduri pe trei statusuri, `403` fără cod cunoscut, cod necunoscut care rămâne la `1`, și că `Hint` nu citează niciodată răspunsul), interogarea OData (apostroful dublat și codarea URL) și faptul că secretul și tokenul nu apar în niciun mesaj de eroare. `PrepareAppointmentItem` este rulat pe toate exemplele din `contracts/examples/valid/`.

Nu ating rețeaua și nu au nevoie de `.env`.

## De verificat

- Codul HTTP pentru Reference duplicat. Codul de eroare este confirmat (`1016` `DuplicateReference`) și e semnalul folosit; codul HTTP rămâne neconfirmat, dar nu contează, pentru că nu se declanșează nimic pe el.
- Forma corpului de eroare (dacă e într-adevăr `{"message": ..., "errorCode": ...}`). Codurile sunt documentate, corpul nu.
- Ce cod întoarce de fapt `AddQueueItem` pentru o coadă inexistentă. `1002` e documentat ca *the* cod de not-found și numește explicit cozile, dar nicio pagină nu mapează acest endpoint pe el.
- Ce răspunde Orchestrator când folderul e greșit **și** coada e deci invizibilă: not-found sau not-authorised. Nedocumentat, iar clientul nu ramifică pe asta.
- Dacă `OR.Queues` ajunge și pentru citirea `QueueItems`, fără alt scope.
- Limita de 128 de caractere pentru `Reference`, fără apostrof (sursă terță).
- `OutputData` (`Output` serializat ca șir) apare în răspunsul Orchestrator sau nu.
