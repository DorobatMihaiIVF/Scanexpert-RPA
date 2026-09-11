using System;
using System.Collections.Generic;
using System.Globalization;

namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// Output-ul pe care robotul îl setează pe itemul din coadă la succes (contract Output v1).
    /// </summary>
    public static class TransactionOutput
    {
        /// <summary>Programarea a fost creată acum în PixelData.</summary>
        public const string OutcomeCreated = "created";

        /// <summary>Verificarea idempotentă a găsit programarea deja în PixelData.</summary>
        public const string OutcomeAlreadyExisted = "already_existed";

        private const string SchemaVersion = "1";
        private const string ProcessedAtFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

        /// <summary>
        /// Dicționarul pentru Output-ul activității Set Transaction Status (Successful). Chei:
        /// OutputSchemaVersion ("1"), Outcome, PatientCreated (bool), PixelDataResource,
        /// ProcessedAt (RFC3339 cu offset, fără fracțiuni de secundă, de exemplu "2026-09-10T14:30:05+03:00").
        /// </summary>
        /// <exception cref="ArgumentException">
        /// outcome nu este "created" sau "already_existed", ori pixelDataResource este gol.
        /// </exception>
        public static Dictionary<string, object> Success(
            string outcome,
            bool patientCreated,
            string pixelDataResource,
            DateTimeOffset processedAt)
        {
            if (!string.Equals(outcome, OutcomeCreated, StringComparison.Ordinal)
                && !string.Equals(outcome, OutcomeAlreadyExisted, StringComparison.Ordinal))
            {
                throw new ArgumentException("outcome trebuie să fie created sau already_existed", nameof(outcome));
            }

            if (string.IsNullOrWhiteSpace(pixelDataResource))
            {
                throw new ArgumentException("pixelDataResource nu poate fi gol", nameof(pixelDataResource));
            }

            Dictionary<string, object> output = new Dictionary<string, object>();
            output.Add("OutputSchemaVersion", SchemaVersion);
            output.Add("Outcome", outcome);
            output.Add("PatientCreated", patientCreated);
            output.Add("PixelDataResource", pixelDataResource);
            output.Add("ProcessedAt", processedAt.ToString(ProcessedAtFormat, CultureInfo.InvariantCulture));
            return output;
        }
    }
}
