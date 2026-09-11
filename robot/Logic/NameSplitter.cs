using System;

namespace PixelDataProgramari.Logic
{
    /// <summary>Împarte numele pacientului în „Nume” și „Prenume” pentru PixelData.</summary>
    public static class NameSplitter
    {
        /// <summary>
        /// Dacă lastName și firstName sunt ambele ne-goale după Trim, sunt folosite ca atare (doar Trim).
        /// Altfel se folosește fullName: se împarte după spații (orice caracter alb, spațiile multiple contează ca unul);
        /// primul cuvânt = LastName („Nume”), restul, unite cu un spațiu = FirstName („Prenume”).
        /// Un singur cuvânt ⇒ FirstName = "". Nimic ⇒ ambele "". null este tratat ca "".
        /// </summary>
        public static PersonName Split(string fullName, string lastName, string firstName)
        {
            string last = (lastName ?? "").Trim();
            string first = (firstName ?? "").Trim();
            if (last.Length > 0 && first.Length > 0)
            {
                return new PersonName(last, first);
            }

            string[] words = (fullName ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return new PersonName("", "");
            }

            string rest = words.Length > 1 ? string.Join(" ", words, 1, words.Length - 1) : "";
            return new PersonName(words[0], rest);
        }
    }
}
