using System;

namespace PixelDataProgramari.Logic
{
    /// <summary>Rezultatul verificării unui CNP cu <see cref="CnpValidator"/>.</summary>
    public sealed class CnpCheckResult
    {
        /// <param name="isValid">True dacă CNP-ul a trecut toate verificările.</param>
        /// <param name="reason">"" dacă e valid; altfel motivul. null devine "".</param>
        /// <param name="birthDate">Data nașterii din CNP, sau null.</param>
        public CnpCheckResult(bool isValid, string reason, DateTime? birthDate)
        {
            IsValid = isValid;
            Reason = reason ?? "";
            BirthDate = birthDate;
        }

        /// <summary>True dacă CNP-ul a trecut toate verificările.</summary>
        public bool IsValid { get; }

        /// <summary>"" dacă CNP-ul e valid; altfel unul dintre textele CnpValidator.Reason*.</summary>
        public string Reason { get; }

        /// <summary>
        /// Data nașterii din CNP. null dacă CNP-ul e invalid sau dacă secolul nu se poate deduce (S = 7, 8 sau 9).
        /// </summary>
        public DateTime? BirthDate { get; }
    }
}
