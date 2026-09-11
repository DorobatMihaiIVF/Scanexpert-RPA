using System;
using System.Collections.Generic;
using System.Text.Json;
using PixelDataProgramari.Logic;

namespace PixelDataProgramari.Logic.Tests
{
    // Date de test fictive. Toate datele programărilor sunt după Samples.Now.
    public static class Samples
    {
        public static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.FromHours(3));

        public const string ValidCnp = "1850312400012";
        public const string WrongChecksumCnp = "1850312400013";

        public static readonly string[] ContractKeys =
        {
            "SchemaVersion", "Operation", "AppointmentId", "CreatedAt", "Source",
            "ScheduledAt", "ScheduledLocalDate", "ScheduledLocalTime", "DurationMinutes",
            "BranchId", "BranchName", "Modality", "ProductId", "ProductCode", "ProductName",
            "Laterality", "Payer", "Insurer", "ReferralDate", "ReferralPending", "ReferralNumber",
            "ReferringDoctorName", "ReferringDoctorClinic", "ReferringDoctorParafa",
            "PatientId", "PatientFullName", "PatientLastName", "PatientFirstName",
            "PatientCnp", "PatientPhone", "PatientBirthDate", "PatientSex", "Notes",
        };

        public static IEnumerable<object[]> ContractKeyData()
        {
            foreach (string key in ContractKeys)
            {
                yield return new object[] { key };
            }
        }

        // Obiectul SpecificContent, cu tipurile pe care le dă JSON-ul (string, bool, număr întreg).
        public static Dictionary<string, object> Content()
        {
            return new Dictionary<string, object>
            {
                { "SchemaVersion", "1" },
                { "Operation", "create" },
                { "AppointmentId", "3f0c9a52-7d1e-4b8a-9c2d-5e6f7a8b9c01" },
                { "CreatedAt", "2027-03-01T10:15:00+02:00" },
                { "Source", "Centrala" },
                { "ScheduledAt", "2027-03-15T09:30:00+02:00" },
                { "ScheduledLocalDate", "2027-03-15" },
                { "ScheduledLocalTime", "09:30" },
                { "DurationMinutes", 30L },
                { "BranchId", "8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d" },
                { "BranchName", "ScanExpert Galați" },
                { "Modality", "RMN" },
                { "ProductId", "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e" },
                { "ProductCode", "TEST-RMN-001" },
                { "ProductName", "RM COLOANA CERVICALA NATIV" },
                { "Laterality", "" },
                { "Payer", "CAS" },
                { "Insurer", "" },
                { "ReferralDate", "2027-02-20" },
                { "ReferralPending", false },
                { "ReferralNumber", "" },
                { "ReferringDoctorName", "DR. TEST MEDIC" },
                { "ReferringDoctorClinic", "CLINICA TEST SRL" },
                { "ReferringDoctorParafa", "T00001" },
                { "PatientId", "c3d4e5f6-a7b8-4c9d-8e0f-2a3b4c5d6e7f" },
                { "PatientFullName", "TEST POPESCU ION" },
                { "PatientLastName", "" },
                { "PatientFirstName", "" },
                { "PatientCnp", ValidCnp },
                { "PatientPhone", "+40700000001" },
                { "PatientBirthDate", "1985-03-12" },
                { "PatientSex", "M" },
                { "Notes", "Programare de test." },
            };
        }

        public static Dictionary<string, object> ContentWith(string key, object value)
        {
            Dictionary<string, object> content = Content();
            content[key] = value;
            return content;
        }

        public static Dictionary<string, object> ContentWithout(string key)
        {
            Dictionary<string, object> content = Content();
            content.Remove(key);
            return content;
        }

        public static string Json(Dictionary<string, object> content)
        {
            return JsonSerializer.Serialize(content);
        }

        // Același item, construit direct (fără parser), pentru validator și mapări.
        public static AppointmentItem Item()
        {
            return new AppointmentItem
            {
                SchemaVersion = "1",
                Operation = "create",
                AppointmentId = "3f0c9a52-7d1e-4b8a-9c2d-5e6f7a8b9c01",
                CreatedAt = new DateTimeOffset(2027, 3, 1, 10, 15, 0, TimeSpan.FromHours(2)),
                Source = "Centrala",
                ScheduledAt = new DateTimeOffset(2027, 3, 15, 9, 30, 0, TimeSpan.FromHours(2)),
                ScheduledLocalDate = new DateTime(2027, 3, 15),
                ScheduledLocalTime = new TimeSpan(9, 30, 0),
                DurationMinutes = 30,
                BranchId = "8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d",
                BranchName = "ScanExpert Galați",
                Modality = "RMN",
                ProductId = "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
                ProductCode = "TEST-RMN-001",
                ProductName = "RM COLOANA CERVICALA NATIV",
                Laterality = "",
                Payer = "CAS",
                Insurer = "",
                ReferralDate = new DateTime(2027, 2, 20),
                ReferralPending = false,
                ReferralNumber = "",
                ReferringDoctorName = "DR. TEST MEDIC",
                ReferringDoctorClinic = "CLINICA TEST SRL",
                ReferringDoctorParafa = "T00001",
                PatientId = "c3d4e5f6-a7b8-4c9d-8e0f-2a3b4c5d6e7f",
                PatientFullName = "TEST POPESCU ION",
                PatientLastName = "",
                PatientFirstName = "",
                PatientCnp = ValidCnp,
                PatientPhone = "+40700000001",
                PatientBirthDate = new DateTime(1985, 3, 12),
                PatientSex = "M",
                Notes = "Programare de test.",
            };
        }
    }
}
