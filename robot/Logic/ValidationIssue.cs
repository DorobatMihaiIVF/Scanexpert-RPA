namespace PixelDataProgramari.Logic
{
    /// <summary>O problemă găsită de <see cref="AppointmentValidator"/>.</summary>
    public sealed class ValidationIssue
    {
        /// <param name="code">Unul dintre codurile din <see cref="ErrorCodes"/>.</param>
        /// <param name="field">Cheia din contract, sau "".</param>
        /// <param name="message">Mesajul, fără codul în față.</param>
        public ValidationIssue(string code, string field, string message)
        {
            Code = code ?? "";
            Field = field ?? "";
            Message = message ?? "";
        }

        /// <summary>Codul de eroare, de exemplu "MISSING_FIELD".</summary>
        public string Code { get; }

        /// <summary>Cheia din contract, de exemplu "ProductName".</summary>
        public string Field { get; }

        /// <summary>Mesaj scurt în română, fără codul în față și fără date ale pacientului.</summary>
        public string Message { get; }
    }
}
