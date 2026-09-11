namespace PixelDataProgramari.Logic
{
    /// <summary>Numele pacientului așa cum îl cere PixelData: „Nume” (de familie) și „Prenume”.</summary>
    public sealed class PersonName
    {
        /// <param name="lastName">„Nume”; null devine "".</param>
        /// <param name="firstName">„Prenume”; null devine "".</param>
        public PersonName(string lastName, string firstName)
        {
            LastName = lastName ?? "";
            FirstName = firstName ?? "";
        }

        /// <summary>„Nume” în PixelData.</summary>
        public string LastName { get; }

        /// <summary>„Prenume” în PixelData; "" dacă numele are un singur cuvânt.</summary>
        public string FirstName { get; }
    }
}
