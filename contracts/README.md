# contracts

Contractul dintre recepția ScanExpert (sau `tools/queue-client`) și robotul `PixelDataProgramari`, pentru coada `PixelData_Programari`. Cheile și regulile vin din `docs/sources/decizii-design-v1.md`.

| Fișier | Ce este |
|---|---|
| `appointment-queue-item.v1.schema.json` | JSON Schema 2020-12 pentru `SpecificContent`, intrarea robotului |
| `queue-item-output.v1.schema.json` | JSON Schema 2020-12 pentru `Output`, setat de robot la succes |
| `examples/valid/` | trec schema. `fara-cnp.json` e respins implicit de robot cu `CNP_REQUIRED` |
| `examples/invalid/schema/` | trebuie să pice schema |
| `examples/invalid/business/` | trec schema, dar logica robotului le respinge cu un cod business |
| `validate_examples.py` | verifică toate exemplele |

## Regula fișierelor exemplu
- Fiecare `examples/**/*.json` conține DOAR obiectul `SpecificContent` plat, niciodată plicul `{"itemData": ...}` trimis la `AddQueueItem`. Plicul îl construiește `tools/queue-client` (și, în faza 2, dispecerul din recepție).
- Date fictive: nume „TEST ...”, telefoane `+4070000000N`, CNP-uri inventate cu cifra de control validă, programări în 2027.

## Ce adaugă schema peste tabel
- Toate cele 33 de chei sunt obligatorii (mereu prezente). Cheile necunoscute sunt permise.
- „Ne-gol” = cel puțin un caracter care nu este spațiu.
- UUID doar cu litere mici.
- Data-ora (`CreatedAt`, `ScheduledAt`, `ProcessedAt`): RFC3339 cu offset obligatoriu (`Z` permis, fracțiune de 1-9 cifre), verificată prin `pattern`, fiindcă `format` nu e aplicat implicit.
- `Insurer` ne-gol când `Payer` = `"Asigurator privat"`.

Ce NU verifică schema (verifică robotul):
- cifra de control a CNP-ului;
- date imposibile în calendar, ex. `2027-02-30`;
- consistența `ScheduledLocalDate` / `ScheduledLocalTime` cu `ScheduledAt` în Europe/Bucharest;
- programări în trecut.

## Rulare
```bash
python3 contracts/validate_examples.py
```
- Merge din orice director. Cere `python3` și pachetul `jsonschema` (folosit cu 4.10.3).
- Coduri de ieșire: `0` totul corect, `1` cel puțin o nepotrivire, `2` eroare de configurare.
- Un folder `invalid/` lipsă sau gol este semnalat, nu e eroare.
- În orice folder e nepotrivire: JSON invalid, chei duplicate, un fișier care nu e obiect sau care e plicul `itemData`.
- Pentru `pattern`, `$` are semantica ECMA-262 cerută de JSON Schema (Python singur ar accepta un `\n` la final).
