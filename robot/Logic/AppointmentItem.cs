using System;

namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// Programarea dintr-un item al cozii PixelData_Programari (contract v1), după parsare cu <see cref="QueueItemParser"/>.
    /// O proprietate pentru fiecare cheie din SpecificContent, cu același nume.
    /// Textele nu sunt niciodată null: "" înseamnă necunoscut.
    /// </summary>
    public sealed class AppointmentItem
    {
        /// <summary>Versiunea contractului. În v1: "1".</summary>
        public string SchemaVersion { get; set; } = "";

        /// <summary>Operația cerută. În v1: "create".</summary>
        public string Operation { get; set; } = "";

        /// <summary>UUID-ul programării în recepție.</summary>
        public string AppointmentId { get; set; } = "";

        /// <summary>Momentul în care programarea a fost creată în recepție.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Canalul programării: "", "Centrala", "Aplicație", "La sediu". Nu este „Sursa pacient” din PixelData.
        /// </summary>
        public string Source { get; set; } = "";

        /// <summary>Data și ora programării, cu offset.</summary>
        public DateTimeOffset ScheduledAt { get; set; }

        /// <summary>Data programării în ora locală Europe/Bucharest (doar data).</summary>
        public DateTime ScheduledLocalDate { get; set; }

        /// <summary>Ora programării în ora locală Europe/Bucharest (ore și minute).</summary>
        public TimeSpan ScheduledLocalTime { get; set; }

        /// <summary>Durata investigației în minute; 0 = necunoscută.</summary>
        public int DurationMinutes { get; set; }

        /// <summary>UUID-ul sucursalei, sau "".</summary>
        public string BranchId { get; set; } = "";

        /// <summary>Numele sucursalei (de exemplu „ScanExpert Galați”); cheia pentru resursa PixelData.</summary>
        public string BranchName { get; set; } = "";

        /// <summary>Modalitatea investigației (de exemplu „RMN”); a doua cheie pentru resursa PixelData.</summary>
        public string Modality { get; set; } = "";

        /// <summary>UUID-ul produsului din catalog, sau "".</summary>
        public string ProductId { get; set; } = "";

        /// <summary>Codul produsului din catalog; cheia pentru procedura PixelData.</summary>
        public string ProductCode { get; set; } = "";

        /// <summary>Numele investigației; folosit ca procedură când codul nu are mapare.</summary>
        public string ProductName { get; set; } = "";

        /// <summary>Lateralitatea: "", "stanga", "dreapta", "bilateral".</summary>
        public string Laterality { get; set; } = "";

        /// <summary>Plătitorul: "", "CAS", "Monitor", "Contra cost", "Asigurator privat".</summary>
        public string Payer { get; set; } = "";

        /// <summary>Asigurătorul privat; obligatoriu doar când Payer = "Asigurator privat".</summary>
        public string Insurer { get; set; } = "";

        /// <summary>„Data bilet”; null = necunoscută.</summary>
        public DateTime? ReferralDate { get; set; }

        /// <summary>True dacă biletul de trimitere nu a sosit încă.</summary>
        public bool ReferralPending { get; set; }

        /// <summary>„Nr./Serie bilet”; în v1 mereu "" (recepția nu îl are).</summary>
        public string ReferralNumber { get; set; } = "";

        /// <summary>„Medic trimițător”.</summary>
        public string ReferringDoctorName { get; set; } = "";

        /// <summary>Clinica medicului trimițător; folosită pentru „Sursa pacient”.</summary>
        public string ReferringDoctorClinic { get; set; } = "";

        /// <summary>Parafa medicului trimițător.</summary>
        public string ReferringDoctorParafa { get; set; } = "";

        /// <summary>UUID-ul pacientului în recepție.</summary>
        public string PatientId { get; set; } = "";

        /// <summary>Numele complet al pacientului, așa cum e în recepție.</summary>
        public string PatientFullName { get; set; } = "";

        /// <summary>„Nume”; "" = se obține din PatientFullName cu <see cref="NameSplitter"/>.</summary>
        public string PatientLastName { get; set; } = "";

        /// <summary>„Prenume”; "" = se obține din PatientFullName cu <see cref="NameSplitter"/>.</summary>
        public string PatientFirstName { get; set; } = "";

        /// <summary>CNP-ul pacientului (13 cifre), sau "".</summary>
        public string PatientCnp { get; set; } = "";

        /// <summary>Telefonul pacientului în format E.164 (+40...), sau "".</summary>
        public string PatientPhone { get; set; } = "";

        /// <summary>Data nașterii; null = necunoscută.</summary>
        public DateTime? PatientBirthDate { get; set; }

        /// <summary>Sexul pacientului, așa cum vine din recepție (valori de verificat).</summary>
        public string PatientSex { get; set; } = "";

        /// <summary>„Observații”; maximum 2000 de caractere.</summary>
        public string Notes { get; set; } = "";
    }
}
