using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// Citește cheile contractului dintr-un SpecificContent și aruncă <see cref="BusinessRuleViolation"/>
    /// cu MISSING_FIELD sau INVALID_FIELD. Mesajele nu conțin valori, fiindcă pot fi date ale pacientului.
    /// </summary>
    internal sealed class SpecificContentReader
    {
        private const int UuidLength = 36;
        private static readonly Regex PhonePattern = new Regex(@"^\+[1-9][0-9]{1,14}\z", RegexOptions.CultureInvariant);

        private readonly IDictionary<string, object> _content;

        internal SpecificContentReader(IDictionary<string, object> content)
        {
            _content = content;
        }

        internal string ReadText(string key)
        {
            object value = ReadPresent(key);
            string text;
            if (!SpecificContentValues.TryToText(value, out text))
            {
                throw Invalid(key, "trebuie să fie text");
            }

            return text;
        }

        internal string ReadRequiredUuid(string key)
        {
            string text = ReadText(key);
            if (!IsUuid(text))
            {
                throw Invalid(key, "trebuie să fie un UUID de forma 8-4-4-4-12");
            }

            return text;
        }

        internal string ReadOptionalUuid(string key)
        {
            string text = ReadText(key);
            if (text.Length > 0 && !IsUuid(text))
            {
                throw Invalid(key, "trebuie să fie gol sau un UUID de forma 8-4-4-4-12");
            }

            return text;
        }

        internal string ReadOneOf(string key, string[] allowed)
        {
            string text = ReadText(key);
            if (Array.IndexOf(allowed, text) < 0)
            {
                throw Invalid(key, "are o valoare pe care contractul nu o permite");
            }

            return text;
        }

        internal string ReadPhone(string key)
        {
            string text = ReadText(key);
            if (text.Length > 0 && !PhonePattern.IsMatch(text))
            {
                throw Invalid(key, "trebuie să fie gol sau un număr în format E.164 (+40...)");
            }

            return text;
        }

        internal string ReadLimitedText(string key, int maxLength)
        {
            string text = ReadText(key);
            if (SpecificContentValues.CodePointCount(text) > maxLength)
            {
                throw Invalid(key, "depășește " + maxLength.ToString(CultureInfo.InvariantCulture) + " de caractere");
            }

            return text;
        }

        internal bool ReadBool(string key)
        {
            object value = ReadPresent(key);
            bool result;
            if (!SpecificContentValues.TryToBool(value, out result))
            {
                throw Invalid(key, "trebuie să fie true sau false");
            }

            return result;
        }

        internal int ReadNonNegativeInt(string key)
        {
            object value = ReadPresent(key);
            int result;
            if (!SpecificContentValues.TryToNonNegativeInt(value, out result))
            {
                throw Invalid(key, "trebuie să fie un număr întreg mai mare sau egal cu 0");
            }

            return result;
        }

        internal DateTimeOffset ReadDateTimeWithOffset(string key)
        {
            object value = ReadPresent(key);
            DateTimeOffset result;
            if (!SpecificContentValues.TryToDateTimeOffset(value, out result))
            {
                throw Invalid(key, "trebuie să fie dată și oră RFC3339 cu offset (Z sau ±HH:MM)");
            }

            return result;
        }

        internal DateTime ReadDate(string key)
        {
            object value = ReadPresent(key);
            DateTime result;
            if (!SpecificContentValues.TryToDate(value, out result))
            {
                throw Invalid(key, "trebuie să fie o dată AAAA-LL-ZZ");
            }

            return result;
        }

        internal DateTime? ReadOptionalDate(string key)
        {
            object value = ReadPresent(key);
            string text = value as string;
            if (text != null && text.Length == 0)
            {
                return null;
            }

            DateTime result;
            if (!SpecificContentValues.TryToDate(value, out result))
            {
                throw Invalid(key, "trebuie să fie gol sau o dată AAAA-LL-ZZ");
            }

            return result;
        }

        internal TimeSpan ReadTime(string key)
        {
            object value = ReadPresent(key);
            string text = value as string;
            if (text == null)
            {
                throw Invalid(key, "trebuie să fie o oră HH:MM");
            }

            TimeSpan result;
            if (!SpecificContentValues.TryParseTime(text, out result))
            {
                throw Invalid(key, "trebuie să fie o oră HH:MM");
            }

            return result;
        }

        // Cheia trebuie să existe și să aibă o valoare simplă, ne-null.
        private object ReadPresent(string key)
        {
            object raw;
            if (!_content.TryGetValue(key, out raw))
            {
                throw new BusinessRuleViolation(ErrorCodes.MissingField, key, "lipsește cheia " + key);
            }

            object value = SpecificContentValues.Normalize(raw);
            if (value == null)
            {
                throw Invalid(key, "nu poate fi null");
            }

            if (value is JsonStructuredValue)
            {
                throw Invalid(key, "trebuie să fie o valoare simplă, nu obiect sau listă");
            }

            return value;
        }

        private static bool IsUuid(string text)
        {
            Guid parsed;
            return text.Length == UuidLength && Guid.TryParseExact(text, "D", out parsed);
        }

        private static BusinessRuleViolation Invalid(string key, string problem)
        {
            return new BusinessRuleViolation(ErrorCodes.InvalidField, key, "cheia " + key + " " + problem);
        }
    }
}
