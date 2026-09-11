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

Reference duplicat: clientul afișează `already queued: create-<AppointmentId>` și iese cu `0`. Coada are Enforce unique references, deci programarea e deja acolo și nu se adaugă un al doilea item. Răspunsul HTTP exact al Orchestrator pentru duplicat nu e confirmat (de verificat): clientul recunoaște orice răspuns ne-2xx al cărui corp conține „duplicate reference”, indiferent de majuscule.

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
| `2` | configurare, argumente sau fișier de intrare greșite |
| `3` | niciun item cu acel Reference (doar `status`) |

## Secrete

- Secretul aplicației și tokenul de acces nu ajung niciodată în ieșire, în erori sau în vreun `Config`/`Client` afișat. Există test pentru asta.
- Tokenul stă doar în memorie, nu se scrie pe disc.
- `.env` este ignorat de git. Dacă secretul ajunge pe un calculator necontrolat, rotește-l din Orchestrator.
- Mesajul unei erori de API numește doar operația și codul HTTP. Corpul răspunsului rămâne pe `APIError.Body` și se afișează doar cu `-debug`, pentru că poate conține date de pacient.
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

Testele pornesc un Orchestrator fals (`net/http/httptest`) și verifică cererea de token, refolosirea tokenului, plicul `itemData` cu headerele lui, duplicatul, interogarea OData (apostroful dublat și codarea URL) și faptul că secretul și tokenul nu apar în niciun mesaj de eroare. `PrepareAppointmentItem` este rulat pe toate exemplele din `contracts/examples/valid/`.

Nu ating rețeaua și nu au nevoie de `.env`.

## De verificat

- Codul HTTP și textul exact pentru Reference duplicat.
- Dacă `OR.Queues` ajunge și pentru citirea `QueueItems`, fără alt scope.
- Limita de 128 de caractere pentru `Reference`, fără apostrof (sursă terță).
- `OutputData` (`Output` serializat ca șir) apare în răspunsul Orchestrator sau nu.
