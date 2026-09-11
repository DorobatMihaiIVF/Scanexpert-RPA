# Flux original: crearea unei programări în PixelData

> Document furnizat de utilizator (analiză cadru-cu-cadru a unui clip cu aplicația PixelData). Păstrat neschimbat ca sursă.

Iată fluxul detaliat și corect pentru crearea unei programări, bazat pe analiza cadru-cu-cadru a interfeței aplicației **PixelData** din clipul furnizat. Acest flux este structurat exact în ordinea acțiunilor pe care un operator uman (și ulterior, robotul UiPath) trebuie să le execute.

### Faza 1: Accesarea Calendarului și a Resurselor

Acesta este punctul de plecare pentru a ajunge la flow-ul de programare.

1. **Navigarea în modulul principal:** În panoul vertical din partea stângă a aplicației, se execută click pe meniul **`PROGRAMĂRI`**. Această acțiune deschide interfața principală de calendar.
2. **Selectarea Datei:** În partea de sus-stânga a ecranului principal (sub tab-urile principale), există un mini-calendar (ex: *August 2020*). Se selectează ziua dorită pentru programare.
3. **Filtrarea Resursei Medicale:** În dreapta calendarului, se folosește un meniu drop-down pentru a selecta resursa, cabinetul sau medicul la care se face programarea (în clip, exemplul selectat este **`C.A.L.A.T.I.`**).

### Faza 2: Selectarea Slotului Orar

1. **Analiza Grilei:** Aplicația afișează un tabel central cu programările zilei. Coloanele vizibile sunt: `Ora`, `Nume`, `Observatii`, `Contact`, `Serviciu` etc.
2. **Alegerea Orei:** Pentru a iniția programarea, se identifică un slot orar liber în grilă.
3. **Deschiderea Fișei de Programare:** Se execută o acțiune (de regulă *Dublu-Click* pe rândul gol corespunzător orei) pentru a deschide interfața de detaliere a vizitei. Aceasta deschide automat tab-ul specific (ex: `C.A.L.A.T.I.`) și sub-tab-ul activ **`Pacient si documente`**.

### Faza 3: Completarea Datelor în "Pacient si documente"

Aceasta este inima fluxului de programare. Odată deschis acest ecran, se parcurg următoarele câmpuri specifice:

1. **Identificarea Pacientului:**
* În partea de sus există câmpul **`Nume/CNP`**. Aici se introduce identificatorul pacientului pentru a-l căuta în baza de date.
* Dacă pacientul este nou, aplicația va necesita deschiderea meniului din stânga, **`Date demografice`**, pentru a adăuga un profil nou. Acolo se completează `Nume`, `Prenume`, `CNP`, dar atenție: dacă CNP-ul introdus este greșit, va apărea o alertă critică cu titlul **`Eroare`** și textul: *"Eroare CNP pacient! CNP incorect. Căutarea returnează eroare"*.

2. **Datele Biletului de Trimitere:**
* Dacă pacientul vine cu trimitere, se bifează checkbox-ul **`Trimitere`**.
* Se completează câmpurile text alăturate pentru **`Nr./Serie bilet`** și se selectează **`Data bilet`**.

3. **Selectarea Sursei:** Se execută click pe meniul drop-down **`Sursa pacient:`** și se alege entitatea corectă dintr-o listă extinsă (în clip, dispecerul alege *TERRA CLINIQUE SRL GALATI*).
4. **Selectarea Medicului Trimițător:** Se execută click pe meniul drop-down **`Medic trimitator:`** și se alege medicul corespunzător (în clip, se alege *DR. CHIRIAC MARIANA*).
5. **Regula Critică de Business - "Starea Pacientului":**
* Există un câmp obligatoriu numit **`Stare:`**. Acesta **trebuie** completat manual (ex: selectând *În lucru*, *Programat* etc.).
* Dacă se sare peste acest câmp și se încearcă salvarea, aplicația va bloca procesul cu un pop-up de **`Atenție`** care afișează mesajul: *"Nu ați selectat starea pacientului!"*. Pentru a continua, trebuie apăsat butonul **`Închide`** al alertei și completată starea.

### Faza 4: Adăugarea Procedurilor Medicale

După completarea datelor administrative din partea de sus, se coboară în jumătatea inferioară a ecranului.

1. **Secțiunea "Proceduri selectate:":** Această zonă conține un tabel unde vor fi listate serviciile medicale pentru acea programare.
2. **Adăugarea Serviciului:** Dispecerul interacționează cu meniul derulant (sau fereastra de căutare a serviciilor) și selectează investigația dorită (în clip, se observă adăugarea procedurii *CONSULTATIE MEDIC SPECIALIST* și a altor proceduri imagistice precum *RM COLOANA CERVICALA NATIV*).
3. Tabelul "Proceduri selectate" trebuie să reflecte serviciul adăugat, cu detalii precum `Cod`, `Denumire`, `Cantitate` și eventual preț.

### Faza 5: Validarea și Finalizarea

1. Odată ce toate câmpurile obligatorii din `Pacient si documente` și tabelul `Proceduri selectate:` sunt completate, se folosește butonul de Salvare/Confirmare (situat de obicei în bara superioară sau inferioară a formularului).
2. Pentru a confirma validitatea, se verifică din nou tabelul principal din **`PROGRAMĂRI`**. Slotul orar ales anterior ar trebui să fie acum ocupat, evidențiat cromatic, afișând numele pacientului, numărul de telefon și serviciul selectat.

**Notă privind ferestrele de confirmare (Pop-ups):** În timpul operării, dacă se greșește sau se dorește anularea acțiunilor în acest ecran, pot apărea ferestre de confirmare pe care robotul trebuie să știe să dea click pe **`DA`** sau **`NU`**:

* **`Atenționare`**: *"Sunteți sigur că doriți eliminarea fișei și revenirea la starea generată?"*
* **`Atenție`**: *"Sunteți sigur că doriți golirea câmpurilor?"*
