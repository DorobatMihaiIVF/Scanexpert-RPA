using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// Marchează o valoare JSON de tip obiect sau listă. Contractul are doar chei plate,
    /// deci o astfel de valoare pe o cheie cunoscută dă INVALID_FIELD.
    /// </summary>
    internal sealed class JsonStructuredValue
    {
        internal static readonly JsonStructuredValue Instance = new JsonStructuredValue();

        private JsonStructuredValue()
        {
        }
    }

    /// <summary>
    /// Conversiile valorilor din SpecificContent. Totul cu CultureInfo.InvariantCulture; regulile sunt descrise
    /// la <see cref="QueueItemParser"/>.
    /// </summary>
    internal static class SpecificContentValues
    {
        private const int FractionDigits = 7;

        private static readonly Regex Rfc3339Pattern = new Regex(
            @"^([0-9]{4})-([0-9]{2})-([0-9]{2})[Tt]([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.([0-9]+))?(?:([Zz])|([+-])([0-9]{2}):([0-9]{2}))\z",
            RegexOptions.CultureInvariant);

        private static readonly Regex DatePattern = new Regex(
            @"^([0-9]{4})-([0-9]{2})-([0-9]{2})\z",
            RegexOptions.CultureInvariant);

        private static readonly Regex TimePattern = new Regex(
            @"^([0-9]{2}):([0-9]{2})\z",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Aduce o valoare la unul dintre tipurile: null, string, bool, long, decimal, double, DateTime,
        /// DateTimeOffset sau JsonStructuredValue.
        /// </summary>
        internal static object Normalize(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is DateTimeOffset || value is JsonStructuredValue)
            {
                return value;
            }

            IConvertible convertible = value as IConvertible;
            if (convertible != null)
            {
                switch (convertible.GetTypeCode())
                {
                    case TypeCode.Empty:
                    case TypeCode.DBNull:
                        return null;
                    case TypeCode.Boolean:
                        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                    case TypeCode.Char:
                    case TypeCode.String:
                        return Convert.ToString(value, CultureInfo.InvariantCulture);
                    case TypeCode.DateTime:
                        return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
                    case TypeCode.SByte:
                    case TypeCode.Byte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    case TypeCode.UInt64:
                    case TypeCode.Decimal:
                        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    case TypeCode.Single:
                    case TypeCode.Double:
                        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
                }
            }

            IFormattable formattable = value as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString() ?? "";
        }

        internal static bool TryToText(object normalized, out string text)
        {
            if (normalized is string s)
            {
                text = s;
                return true;
            }

            if (normalized is bool b)
            {
                text = b ? "true" : "false";
                return true;
            }

            if (normalized is long l)
            {
                text = l.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (normalized is decimal m)
            {
                text = m.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (normalized is double d)
            {
                text = d.ToString("R", CultureInfo.InvariantCulture);
                return true;
            }

            if (normalized is DateTimeOffset dto)
            {
                text = FormatDateTimeOffset(dto);
                return true;
            }

            if (normalized is DateTime dt)
            {
                text = FormatDateTime(dt);
                return true;
            }

            text = "";
            return false;
        }

        internal static bool TryToBool(object normalized, out bool result)
        {
            if (normalized is bool b)
            {
                result = b;
                return true;
            }

            if (normalized is string text)
            {
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    return true;
                }

                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
                {
                    result = false;
                    return true;
                }
            }

            result = false;
            return false;
        }

        internal static bool TryToNonNegativeInt(object normalized, out int result)
        {
            result = 0;
            if (normalized is long l)
            {
                if (l < 0 || l > int.MaxValue)
                {
                    return false;
                }

                result = (int)l;
                return true;
            }

            if (normalized is double d)
            {
                if (double.IsNaN(d) || double.IsInfinity(d) || d < 0 || d > int.MaxValue || Math.Floor(d) != d)
                {
                    return false;
                }

                result = (int)d;
                return true;
            }

            if (normalized is decimal m)
            {
                if (m < 0 || m > int.MaxValue || decimal.Truncate(m) != m)
                {
                    return false;
                }

                result = (int)m;
                return true;
            }

            if (normalized is string text)
            {
                int parsed;
                if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out parsed))
                {
                    return false;
                }

                result = parsed;
                return true;
            }

            return false;
        }

        internal static bool TryToDateTimeOffset(object normalized, out DateTimeOffset result)
        {
            if (normalized is DateTimeOffset dto)
            {
                result = dto;
                return true;
            }

            if (normalized is DateTime dt)
            {
                result = default(DateTimeOffset);
                if (dt.Kind == DateTimeKind.Unspecified)
                {
                    return false;
                }

                try
                {
                    result = new DateTimeOffset(dt);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            if (normalized is string text)
            {
                return TryParseRfc3339(text, out result);
            }

            result = default(DateTimeOffset);
            return false;
        }

        internal static bool TryToDate(object normalized, out DateTime result)
        {
            if (normalized is DateTime dt)
            {
                result = DateTime.SpecifyKind(dt.Date, DateTimeKind.Unspecified);
                return true;
            }

            if (normalized is DateTimeOffset dto)
            {
                result = dto.Date;
                return true;
            }

            if (normalized is string text)
            {
                return TryParseDate(text, out result);
            }

            result = default(DateTime);
            return false;
        }

        // yyyy-MM-dd[Tt]HH:mm:ss[.fracțiune][Zz|±HH:MM]; fracțiunea este trunchiată la 7 cifre (ticks).
        internal static bool TryParseRfc3339(string text, out DateTimeOffset result)
        {
            result = default(DateTimeOffset);
            Match match = Rfc3339Pattern.Match(text);
            if (!match.Success)
            {
                return false;
            }

            long fractionTicks = 0;
            if (match.Groups[7].Success)
            {
                string digits = match.Groups[7].Value;
                if (digits.Length > FractionDigits)
                {
                    digits = digits.Substring(0, FractionDigits);
                }

                fractionTicks = long.Parse(
                    digits.PadRight(FractionDigits, '0'),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture);
            }

            TimeSpan offset = TimeSpan.Zero;
            if (!match.Groups[8].Success)
            {
                int offsetHours = GroupInt(match, 10);
                int offsetMinutes = GroupInt(match, 11);
                if (offsetHours > 23 || offsetMinutes > 59)
                {
                    return false;
                }

                offset = new TimeSpan(offsetHours, offsetMinutes, 0);
                if (match.Groups[9].Value == "-")
                {
                    offset = offset.Negate();
                }
            }

            try
            {
                result = new DateTimeOffset(
                    GroupInt(match, 1),
                    GroupInt(match, 2),
                    GroupInt(match, 3),
                    GroupInt(match, 4),
                    GroupInt(match, 5),
                    GroupInt(match, 6),
                    offset).AddTicks(fractionTicks);
                return true;
            }
            catch (ArgumentException)
            {
                result = default(DateTimeOffset);
                return false;
            }
        }

        internal static bool TryParseDate(string text, out DateTime result)
        {
            result = default(DateTime);
            Match match = DatePattern.Match(text);
            if (!match.Success)
            {
                return false;
            }

            int year = GroupInt(match, 1);
            int month = GroupInt(match, 2);
            int day = GroupInt(match, 3);
            if (year < 1 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                return false;
            }

            result = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
            return true;
        }

        internal static bool TryParseTime(string text, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            Match match = TimePattern.Match(text);
            if (!match.Success)
            {
                return false;
            }

            int hours = GroupInt(match, 1);
            int minutes = GroupInt(match, 2);
            if (hours > 23 || minutes > 59)
            {
                return false;
            }

            result = new TimeSpan(hours, minutes, 0);
            return true;
        }

        // Numărul de caractere Unicode (o pereche surogat contează o dată), ca maxLength din JSON Schema.
        internal static int CodePointCount(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                }

                count++;
            }

            return count;
        }

        private static string FormatDateTimeOffset(DateTimeOffset value)
        {
            return value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);
        }

        private static string FormatDateTime(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);
            }

            if (value.Kind == DateTimeKind.Local)
            {
                try
                {
                    return FormatDateTimeOffset(new DateTimeOffset(value));
                }
                catch (ArgumentException)
                {
                    return value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
                }
            }

            if (value.TimeOfDay == TimeSpan.Zero)
            {
                return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
        }

        private static int GroupInt(Match match, int group)
        {
            return int.Parse(match.Groups[group].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        }
    }
}
