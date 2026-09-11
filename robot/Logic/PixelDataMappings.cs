using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// Fișierul robot/Data/pixeldata-mappings.json: corespondența dintre datele recepției și valorile din PixelData.
    /// </summary>
    /// <remarks>
    /// Toate valorile se compară după Trim, fără diferență între majuscule și minuscule (OrdinalIgnoreCase).
    /// "TODO" și "" înseamnă valoare lipsă.
    /// Un fișier greșit (JSON invalid, tip greșit, mappingsVersion diferit de "1", initialStatus lipsă sau TODO,
    /// patientSource lipsă, strategie necunoscută) aruncă FormatException la încărcare: este o eroare de configurare,
    /// nu de business. Sunt permise comentarii și virgula după ultimul element.
    /// </remarks>
    public sealed class PixelDataMappings
    {
        private const string SupportedMappingsVersion = "1";
        private const string Todo = "TODO";
        private const string Wildcard = "*";
        private const string StrategyReferringDoctorClinic = "referringDoctorClinic";
        private const string StrategyFixed = "fixed";

        private readonly List<string> _referralPayers;
        private readonly List<ResourceRow> _resources;
        private readonly string _patientSourceStrategy;
        private readonly string _patientSourceFallback;
        private readonly List<ClinicAliasRow> _clinicAliases;
        private readonly List<ProcedureRow> _procedures;

        private PixelDataMappings(
            string mappingsVersion,
            string initialStatus,
            List<string> referralPayers,
            List<ResourceRow> resources,
            string patientSourceStrategy,
            string patientSourceFallback,
            List<ClinicAliasRow> clinicAliases,
            List<ProcedureRow> procedures)
        {
            MappingsVersion = mappingsVersion;
            InitialStatus = initialStatus;
            _referralPayers = referralPayers;
            _resources = resources;
            _patientSourceStrategy = patientSourceStrategy;
            _patientSourceFallback = patientSourceFallback;
            _clinicAliases = clinicAliases;
            _procedures = procedures;
        }

        /// <summary>Versiunea fișierului; "1".</summary>
        public string MappingsVersion { get; }

        /// <summary>Valoarea pentru „Stare:” din PixelData (de exemplu „Programat”).</summary>
        public string InitialStatus { get; }

        /// <summary>Încarcă fișierul de mapări din textul lui JSON.</summary>
        /// <exception cref="ArgumentNullException">json este null.</exception>
        /// <exception cref="FormatException">Fișierul nu respectă structura așteptată.</exception>
        public static PixelDataMappings FromJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            JsonDocumentOptions options = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            };

            try
            {
                using (JsonDocument document = JsonDocument.Parse(json, options))
                {
                    return FromRoot(document.RootElement);
                }
            }
            catch (JsonException ex)
            {
                throw new FormatException("pixeldata-mappings.json nu este JSON valid: " + ex.Message, ex);
            }
        }

        /// <summary>True dacă plătitorul cere „Trimitere” (este în referralPayers).</summary>
        public bool NeedsReferral(string payer)
        {
            string value = (payer ?? "").Trim();
            if (value.Length == 0)
            {
                return false;
            }

            foreach (string referralPayer in _referralPayers)
            {
                if (SameText(referralPayer, value))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resursa PixelData pentru sucursală și modalitate. Rândul cu modalitatea exactă are prioritate față de "*".
        /// Rândul ales cu "TODO" sau gol nu cade pe "*".
        /// </summary>
        /// <exception cref="BusinessRuleViolation">RESOURCE_MAPPING_MISSING (Field "BranchName").</exception>
        public string ResolveResource(string branchName, string modality)
        {
            string branch = (branchName ?? "").Trim();
            string itemModality = (modality ?? "").Trim();

            ResourceRow match = null;
            foreach (ResourceRow row in _resources)
            {
                if (row.Modality != Wildcard && SameText(row.BranchName, branch) && SameText(row.Modality, itemModality))
                {
                    match = row;
                    break;
                }
            }

            if (match == null)
            {
                foreach (ResourceRow row in _resources)
                {
                    if (row.Modality == Wildcard && SameText(row.BranchName, branch))
                    {
                        match = row;
                        break;
                    }
                }
            }

            if (match == null || IsMissing(match.PixelDataResource))
            {
                throw new BusinessRuleViolation(
                    ErrorCodes.ResourceMappingMissing,
                    "BranchName",
                    "nu există resursă PixelData în pixeldata-mappings.json pentru sucursala „" + branch
                        + "” și modalitatea „" + itemModality + "”");
            }

            return match.PixelDataResource;
        }

        /// <summary>
        /// Valoarea pentru „Sursa pacient:”. Strategia "fixed" folosește mereu fallback. Strategia
        /// "referringDoctorClinic" caută clinica medicului trimițător în clinicAliases; dacă nu o găsește
        /// (sau clinica e goală) folosește fallback.
        /// </summary>
        /// <exception cref="ArgumentNullException">item este null.</exception>
        /// <exception cref="BusinessRuleViolation">PATIENT_SOURCE_MAPPING_MISSING (Field "ReferringDoctorClinic").</exception>
        public string ResolvePatientSource(AppointmentItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            string clinic = (item.ReferringDoctorClinic ?? "").Trim();
            string result = _patientSourceFallback;
            if (_patientSourceStrategy == StrategyReferringDoctorClinic && clinic.Length > 0)
            {
                foreach (ClinicAliasRow alias in _clinicAliases)
                {
                    if (SameText(alias.Clinic, clinic))
                    {
                        result = alias.PixelDataSource;
                        break;
                    }
                }
            }

            if (IsMissing(result))
            {
                throw new BusinessRuleViolation(
                    ErrorCodes.PatientSourceMappingMissing,
                    "ReferringDoctorClinic",
                    "„Sursa pacient” nu este configurată în pixeldata-mappings.json (strategia " + _patientSourceStrategy
                        + ", clinica „" + clinic + "”)");
            }

            return result;
        }

        /// <summary>
        /// Denumirea procedurii în PixelData: maparea codului de produs, dacă există și nu e "TODO";
        /// altfel productName (după Trim). Nu aruncă.
        /// </summary>
        public string ResolveProcedure(string productCode, string productName)
        {
            string code = (productCode ?? "").Trim();
            if (code.Length > 0)
            {
                foreach (ProcedureRow row in _procedures)
                {
                    if (SameText(row.ProductCode, code))
                    {
                        if (!IsMissing(row.PixelDataProcedure))
                        {
                            return row.PixelDataProcedure;
                        }

                        break;
                    }
                }
            }

            return (productName ?? "").Trim();
        }

        private static PixelDataMappings FromRoot(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("rădăcina trebuie să fie un obiect JSON");
            }

            string mappingsVersion = ReadString(root, "mappingsVersion", "mappingsVersion").Trim();
            if (mappingsVersion != SupportedMappingsVersion)
            {
                throw Invalid("mappingsVersion trebuie să fie \"1\"");
            }

            string initialStatus = ReadString(root, "initialStatus", "initialStatus").Trim();
            if (IsMissing(initialStatus))
            {
                throw Invalid("initialStatus lipsește sau este TODO");
            }

            List<string> referralPayers = new List<string>();
            foreach (JsonElement payer in ReadArray(root, "referralPayers", "referralPayers"))
            {
                if (payer.ValueKind != JsonValueKind.String)
                {
                    throw Invalid("referralPayers trebuie să conțină doar texte");
                }

                referralPayers.Add(payer.GetString().Trim());
            }

            List<ResourceRow> resources = new List<ResourceRow>();
            List<JsonElement> resourceElements = ReadArray(root, "resources", "resources");
            for (int i = 0; i < resourceElements.Count; i++)
            {
                string path = ItemPath("resources", i);
                JsonElement row = RequireObject(resourceElements[i], path);
                resources.Add(new ResourceRow(
                    ReadString(row, "branchName", path + ".branchName").Trim(),
                    ReadString(row, "modality", path + ".modality").Trim(),
                    ReadString(row, "pixelDataResource", path + ".pixelDataResource").Trim()));
            }

            JsonElement patientSource;
            if (!root.TryGetProperty("patientSource", out patientSource) || patientSource.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("patientSource lipsește sau nu este un obiect");
            }

            string strategyText = ReadString(patientSource, "strategy", "patientSource.strategy").Trim();
            string strategy;
            if (SameText(strategyText, StrategyReferringDoctorClinic))
            {
                strategy = StrategyReferringDoctorClinic;
            }
            else if (SameText(strategyText, StrategyFixed))
            {
                strategy = StrategyFixed;
            }
            else
            {
                throw Invalid("patientSource.strategy trebuie să fie referringDoctorClinic sau fixed");
            }

            string fallback = ReadString(patientSource, "fallback", "patientSource.fallback").Trim();

            List<ClinicAliasRow> clinicAliases = new List<ClinicAliasRow>();
            List<JsonElement> aliasElements = ReadArray(patientSource, "clinicAliases", "patientSource.clinicAliases");
            for (int i = 0; i < aliasElements.Count; i++)
            {
                string path = ItemPath("patientSource.clinicAliases", i);
                JsonElement row = RequireObject(aliasElements[i], path);
                clinicAliases.Add(new ClinicAliasRow(
                    ReadString(row, "clinic", path + ".clinic").Trim(),
                    ReadString(row, "pixelDataSource", path + ".pixelDataSource").Trim()));
            }

            List<ProcedureRow> procedures = new List<ProcedureRow>();
            List<JsonElement> procedureElements = ReadArray(root, "procedures", "procedures");
            for (int i = 0; i < procedureElements.Count; i++)
            {
                string path = ItemPath("procedures", i);
                JsonElement row = RequireObject(procedureElements[i], path);
                procedures.Add(new ProcedureRow(
                    ReadString(row, "productCode", path + ".productCode").Trim(),
                    ReadString(row, "pixelDataProcedure", path + ".pixelDataProcedure").Trim()));
            }

            return new PixelDataMappings(
                mappingsVersion,
                initialStatus,
                referralPayers,
                resources,
                strategy,
                fallback,
                clinicAliases,
                procedures);
        }

        // Proprietate absentă sau null ⇒ listă goală.
        private static List<JsonElement> ReadArray(JsonElement parent, string name, string path)
        {
            List<JsonElement> items = new List<JsonElement>();
            JsonElement value;
            if (!parent.TryGetProperty(name, out value) || value.ValueKind == JsonValueKind.Null)
            {
                return items;
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                throw Invalid(path + " trebuie să fie o listă");
            }

            foreach (JsonElement item in value.EnumerateArray())
            {
                items.Add(item);
            }

            return items;
        }

        // Proprietate absentă sau null ⇒ "".
        private static string ReadString(JsonElement parent, string name, string path)
        {
            JsonElement value;
            if (!parent.TryGetProperty(name, out value) || value.ValueKind == JsonValueKind.Null)
            {
                return "";
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                throw Invalid(path + " trebuie să fie text");
            }

            return value.GetString();
        }

        private static JsonElement RequireObject(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw Invalid(path + " trebuie să fie un obiect");
            }

            return element;
        }

        private static string ItemPath(string name, int index)
        {
            return name + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
        }

        private static bool IsMissing(string value)
        {
            return value.Length == 0 || string.Equals(value, Todo, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameText(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static FormatException Invalid(string problem)
        {
            return new FormatException("pixeldata-mappings.json: " + problem);
        }

        private sealed class ResourceRow
        {
            internal ResourceRow(string branchName, string modality, string pixelDataResource)
            {
                BranchName = branchName;
                Modality = modality;
                PixelDataResource = pixelDataResource;
            }

            internal string BranchName { get; }

            internal string Modality { get; }

            internal string PixelDataResource { get; }
        }

        private sealed class ClinicAliasRow
        {
            internal ClinicAliasRow(string clinic, string pixelDataSource)
            {
                Clinic = clinic;
                PixelDataSource = pixelDataSource;
            }

            internal string Clinic { get; }

            internal string PixelDataSource { get; }
        }

        private sealed class ProcedureRow
        {
            internal ProcedureRow(string productCode, string pixelDataProcedure)
            {
                ProductCode = productCode;
                PixelDataProcedure = pixelDataProcedure;
            }

            internal string ProductCode { get; }

            internal string PixelDataProcedure { get; }
        }
    }
}
