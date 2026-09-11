using System;

namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// O regulă de business încălcată, cu cod de eroare. Message = "COD: mesaj".
    /// În XAML se transformă în excepția de business UiPath:
    /// catch BusinessRuleViolation ex ⇒ throw new BusinessRuleException(ex.Message).
    /// </summary>
    public sealed class BusinessRuleViolation : Exception
    {
        /// <param name="code">Unul dintre codurile din <see cref="ErrorCodes"/>.</param>
        /// <param name="field">Cheia din contract la care se referă problema, sau "".</param>
        /// <param name="message">Mesaj scurt în română, fără date ale pacientului.</param>
        public BusinessRuleViolation(string code, string field, string message)
            : base((code ?? "") + ": " + (message ?? ""))
        {
            Code = code ?? "";
            Field = field ?? "";
        }

        /// <summary>Codul de eroare, de exemplu "CNP_INVALID".</summary>
        public string Code { get; }

        /// <summary>Cheia din contract (de exemplu "PatientCnp"), sau "".</summary>
        public string Field { get; }
    }
}
