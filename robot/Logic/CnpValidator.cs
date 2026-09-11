using System;

namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// Verifică un CNP (Cod Numeric Personal) cu structura S AA LL ZZ JJ NNN C.
    /// </summary>
    /// <remarks>
    /// Ordinea verificărilor: gol, 13 caractere, doar cifre, S diferit de 0, luna 01-12, ziua validă,
    /// cifra de control (ponderi 279146358279, suma modulo 11, restul 10 devine 1; aceeași regulă ca validCNP din recepție).
    /// Mai strict decât recepția: verifică și S, luna și ziua.
    /// Nu verifică județul (JJ): lista din investigație este neconfirmată, iar PixelData are ultimul cuvânt
    /// (PIXELDATA_CNP_REJECTED).
    /// Secolul: S 1/2 = 1900-1999, 3/4 = 1800-1899, 5/6 = 2000-2099.
    /// S 7/8/9 (rezidenți străini, străini): secolul nu se poate deduce din CNP. Ziua este acceptată dacă este validă
    /// în cel puțin unul dintre secolele 18AA, 19AA, 20AA, iar BirthDate este null.
    /// Nu se face Trim: un spațiu în CNP îl face invalid.
    /// </remarks>
    public static class CnpValidator
    {
        /// <summary>CNP null sau "".</summary>
        public const string ReasonEmpty = "CNP gol";

        /// <summary>CNP-ul nu are exact 13 caractere.</summary>
        public const string ReasonLength = "CNP-ul nu are 13 caractere";

        /// <summary>CNP-ul conține altceva decât cifrele 0-9.</summary>
        public const string ReasonNotDigits = "CNP-ul conține caractere care nu sunt cifre";

        /// <summary>Prima cifră (sex și secol) este 0.</summary>
        public const string ReasonFirstDigitZero = "prima cifră (S) nu poate fi 0";

        /// <summary>Luna (LL) nu este între 01 și 12.</summary>
        public const string ReasonMonth = "luna din CNP nu este între 01 și 12";

        /// <summary>Ziua (ZZ) nu există în luna respectivă.</summary>
        public const string ReasonDay = "ziua din CNP nu este validă pentru luna respectivă";

        /// <summary>Cifra de control (ultima) nu corespunde.</summary>
        public const string ReasonChecksum = "cifra de control nu corespunde";

        private const int CnpLength = 13;
        private static readonly int[] Weights = { 2, 7, 9, 1, 4, 6, 3, 5, 8, 2, 7, 9 };

        /// <summary>Verifică CNP-ul și, dacă e valid, extrage data nașterii.</summary>
        public static CnpCheckResult Check(string cnp)
        {
            if (string.IsNullOrEmpty(cnp))
            {
                return Invalid(ReasonEmpty);
            }

            if (cnp.Length != CnpLength)
            {
                return Invalid(ReasonLength);
            }

            for (int i = 0; i < cnp.Length; i++)
            {
                if (cnp[i] < '0' || cnp[i] > '9')
                {
                    return Invalid(ReasonNotDigits);
                }
            }

            int sexAndCentury = Digit(cnp, 0);
            if (sexAndCentury == 0)
            {
                return Invalid(ReasonFirstDigitZero);
            }

            int yearInCentury = TwoDigits(cnp, 1);
            int month = TwoDigits(cnp, 3);
            int day = TwoDigits(cnp, 5);
            if (month < 1 || month > 12)
            {
                return Invalid(ReasonMonth);
            }

            int century = CenturyStart(sexAndCentury);
            bool dayIsValid = century > 0
                ? IsValidDay(century + yearInCentury, month, day)
                : IsValidDay(1800 + yearInCentury, month, day)
                    || IsValidDay(1900 + yearInCentury, month, day)
                    || IsValidDay(2000 + yearInCentury, month, day);
            if (!dayIsValid)
            {
                return Invalid(ReasonDay);
            }

            int sum = 0;
            for (int i = 0; i < Weights.Length; i++)
            {
                sum += Digit(cnp, i) * Weights[i];
            }

            int control = sum % 11;
            if (control == 10)
            {
                control = 1;
            }

            if (Digit(cnp, 12) != control)
            {
                return Invalid(ReasonChecksum);
            }

            DateTime? birthDate = null;
            if (century > 0)
            {
                birthDate = new DateTime(century + yearInCentury, month, day);
            }

            return new CnpCheckResult(true, "", birthDate);
        }

        /// <summary>Echivalent cu Check(cnp).IsValid.</summary>
        public static bool IsValid(string cnp)
        {
            return Check(cnp).IsValid;
        }

        // Primul an al secolului pentru cifra S; 0 = secol necunoscut (S = 7, 8, 9).
        private static int CenturyStart(int sexAndCentury)
        {
            switch (sexAndCentury)
            {
                case 1:
                case 2:
                    return 1900;
                case 3:
                case 4:
                    return 1800;
                case 5:
                case 6:
                    return 2000;
                default:
                    return 0;
            }
        }

        private static bool IsValidDay(int year, int month, int day)
        {
            return day >= 1 && day <= DateTime.DaysInMonth(year, month);
        }

        private static int Digit(string cnp, int index)
        {
            return cnp[index] - '0';
        }

        private static int TwoDigits(string cnp, int index)
        {
            return (Digit(cnp, index) * 10) + Digit(cnp, index + 1);
        }

        private static CnpCheckResult Invalid(string reason)
        {
            return new CnpCheckResult(false, reason, null);
        }
    }
}
