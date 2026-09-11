using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// Transformă SpecificContent-ul unui item din coada PixelData_Programari (contract v1) în <see cref="AppointmentItem"/>.
    /// </summary>
    /// <remarks>
    /// Ordinea verificărilor: SchemaVersion, Operation, apoi celelalte chei în ordinea tabelului din contract.
    /// Prima problemă găsită aruncă <see cref="BusinessRuleViolation"/>:
    /// MISSING_FIELD (cheia lipsește), INVALID_FIELD (valoare null sau format greșit),
    /// UNSUPPORTED_SCHEMA_VERSION (SchemaVersion diferit de "1"), UNSUPPORTED_OPERATION (Operation diferit de "create").
    /// Cheile necunoscute sunt ignorate. Mesajele nu conțin valori din item.
    ///
    /// Valorile acceptate (de verificat pe laptop). În Studio, Robot-ul deserializează SpecificContent cu Newtonsoft,
    /// deci o valoare poate fi string, bool, long, double, DateTime sau JValue, iar un text ISO-8601 cu oră poate
    /// ajunge deja ca DateTime (Kind Local sau Utc), al cărui ToString() depinde de cultură și pierde offset-ul.
    /// Pas 1, normalizarea: DateTimeOffset rămâne ca atare. Un IConvertible (inclusiv JValue) se citește după
    /// GetTypeCode(): Empty/DBNull = null; Boolean = bool; String/Char = text;
    /// DateTime = Convert.ToDateTime(value, CultureInfo.InvariantCulture); întregi până la Int64 = long;
    /// UInt64 și Decimal = decimal; Single și Double = double. Alt IFormattable: ToString(null, InvariantCulture).
    /// Orice altceva: ToString().
    /// Pas 2, pe tipul câmpului:
    /// - text: string ca atare (fără Trim); bool = "true"/"false"; numere în formă invariantă;
    ///   DateTimeOffset și DateTime = text ISO-8601.
    /// - dată și oră (CreatedAt, ScheduledAt): DateTimeOffset ca atare; DateTime cu Kind Utc sau Local devine
    ///   DateTimeOffset; DateTime cu Kind Unspecified = INVALID_FIELD (nu are offset); text RFC3339 cu offset (Z sau ±HH:MM).
    /// - doar dată (ScheduledLocalDate, ReferralDate, PatientBirthDate): DateTime = partea de dată (.Date);
    ///   DateTimeOffset = .Date; text AAAA-LL-ZZ; "" = null doar la ReferralDate și PatientBirthDate.
    /// - oră (ScheduledLocalTime): doar text HH:MM.
    /// - întreg (DurationMinutes): număr întreg mai mare sau egal cu 0 (long, sau double/decimal fără zecimale),
    ///   sau text format doar din cifre.
    /// - bool (ReferralPending): bool, sau text "true"/"false" (majusculele nu contează).
    /// Formate verificate: UUID la AppointmentId, PatientId (și la BranchId, ProductId când nu sunt goale);
    /// listele de valori la Source, Laterality, Payer; E.164 la PatientPhone; maximum 2000 de caractere la Notes.
    /// PatientCnp nu este verificat aici: <see cref="AppointmentValidator"/> dă CNP_INVALID.
    /// </remarks>
    public static class QueueItemParser
    {
        private const string SupportedSchemaVersion = "1";
        private const string SupportedOperation = "create";
        private const int NotesMaxLength = 2000;

        // "Aplicație" cu ț (U+021B), scris cu escape ca să nu depindă de codificarea fișierului.
        private static readonly string[] Sources = { "", "Centrala", "Aplicație", "La sediu" };
        private static readonly string[] Lateralities = { "", "stanga", "dreapta", "bilateral" };
        private static readonly string[] Payers = { "", "CAS", "Monitor", "Contra cost", "Asigurator privat" };

        /// <summary>
        /// Parsează in_TransactionItem.SpecificContent (Studio). Regulile sunt în descrierea clasei.
        /// </summary>
        /// <exception cref="ArgumentNullException">specificContent este null.</exception>
        /// <exception cref="BusinessRuleViolation">Itemul nu respectă contractul v1.</exception>
        public static AppointmentItem FromSpecificContent(IDictionary<string, object> specificContent)
        {
            if (specificContent == null)
            {
                throw new ArgumentNullException(nameof(specificContent));
            }

            SpecificContentReader reader = new SpecificContentReader(specificContent);
            AppointmentItem item = new AppointmentItem();

            item.SchemaVersion = reader.ReadText("SchemaVersion");
            if (item.SchemaVersion != SupportedSchemaVersion)
            {
                throw new BusinessRuleViolation(
                    ErrorCodes.UnsupportedSchemaVersion,
                    "SchemaVersion",
                    "robotul acceptă doar SchemaVersion 1");
            }

            item.Operation = reader.ReadText("Operation");
            if (item.Operation != SupportedOperation)
            {
                throw new BusinessRuleViolation(
                    ErrorCodes.UnsupportedOperation,
                    "Operation",
                    "robotul acceptă doar Operation create");
            }

            item.AppointmentId = reader.ReadRequiredUuid("AppointmentId");
            item.CreatedAt = reader.ReadDateTimeWithOffset("CreatedAt");
            item.Source = reader.ReadOneOf("Source", Sources);
            item.ScheduledAt = reader.ReadDateTimeWithOffset("ScheduledAt");
            item.ScheduledLocalDate = reader.ReadDate("ScheduledLocalDate");
            item.ScheduledLocalTime = reader.ReadTime("ScheduledLocalTime");
            item.DurationMinutes = reader.ReadNonNegativeInt("DurationMinutes");
            item.BranchId = reader.ReadOptionalUuid("BranchId");
            item.BranchName = reader.ReadText("BranchName");
            item.Modality = reader.ReadText("Modality");
            item.ProductId = reader.ReadOptionalUuid("ProductId");
            item.ProductCode = reader.ReadText("ProductCode");
            item.ProductName = reader.ReadText("ProductName");
            item.Laterality = reader.ReadOneOf("Laterality", Lateralities);
            item.Payer = reader.ReadOneOf("Payer", Payers);
            item.Insurer = reader.ReadText("Insurer");
            item.ReferralDate = reader.ReadOptionalDate("ReferralDate");
            item.ReferralPending = reader.ReadBool("ReferralPending");
            item.ReferralNumber = reader.ReadText("ReferralNumber");
            item.ReferringDoctorName = reader.ReadText("ReferringDoctorName");
            item.ReferringDoctorClinic = reader.ReadText("ReferringDoctorClinic");
            item.ReferringDoctorParafa = reader.ReadText("ReferringDoctorParafa");
            item.PatientId = reader.ReadRequiredUuid("PatientId");
            item.PatientFullName = reader.ReadText("PatientFullName");
            item.PatientLastName = reader.ReadText("PatientLastName");
            item.PatientFirstName = reader.ReadText("PatientFirstName");
            item.PatientCnp = reader.ReadText("PatientCnp");
            item.PatientPhone = reader.ReadPhone("PatientPhone");
            item.PatientBirthDate = reader.ReadOptionalDate("PatientBirthDate");
            item.PatientSex = reader.ReadText("PatientSex");
            item.Notes = reader.ReadLimitedText("Notes", NotesMaxLength);
            return item;
        }

        /// <summary>
        /// Parsează JSON-ul obiectului SpecificContent (fără învelișul itemData), pentru teste și instrumente.
        /// Numerele devin long, apoi decimal, apoi double; null rămâne null; un obiect sau o listă pe o cheie
        /// cunoscută dă INVALID_FIELD. Apoi se aplică aceleași reguli ca la <see cref="FromSpecificContent"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">specificContentJson este null.</exception>
        /// <exception cref="BusinessRuleViolation">
        /// JSON invalid sau rădăcina nu este obiect (INVALID_FIELD, Field ""), ori itemul nu respectă contractul v1.
        /// </exception>
        public static AppointmentItem FromJson(string specificContentJson)
        {
            if (specificContentJson == null)
            {
                throw new ArgumentNullException(nameof(specificContentJson));
            }

            Dictionary<string, object> content = new Dictionary<string, object>(StringComparer.Ordinal);
            try
            {
                using (JsonDocument document = JsonDocument.Parse(specificContentJson))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        throw new BusinessRuleViolation(
                            ErrorCodes.InvalidField,
                            "",
                            "SpecificContent trebuie să fie un obiect JSON");
                    }

                    foreach (JsonProperty property in document.RootElement.EnumerateObject())
                    {
                        content[property.Name] = FromJsonElement(property.Value);
                    }
                }
            }
            catch (JsonException)
            {
                throw new BusinessRuleViolation(ErrorCodes.InvalidField, "", "SpecificContent nu este JSON valid");
            }

            return FromSpecificContent(content);
        }

        private static object FromJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Number:
                {
                    long integer;
                    if (element.TryGetInt64(out integer))
                    {
                        return integer;
                    }

                    decimal number;
                    if (element.TryGetDecimal(out number))
                    {
                        return number;
                    }

                    double real;
                    if (element.TryGetDouble(out real))
                    {
                        return real;
                    }

                    return JsonStructuredValue.Instance;
                }

                default:
                    return JsonStructuredValue.Instance;
            }
        }
    }
}
