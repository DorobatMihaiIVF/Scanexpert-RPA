using System;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    public class PixelDataMappingsTests
    {
        // Apostroful devine ghilimea: JSON lizibil în C# 10, fără raw string literals.
        private static string Q(string singleQuotedJson)
        {
            return singleQuotedJson.Replace('\'', '"');
        }

        private static string ResourcesJson(string resources)
        {
            return Q(@"{
  'mappingsVersion': '1',
  'initialStatus': 'Programat',
  'referralPayers': ['CAS', 'Monitor'],
  'resources': [" + resources + @"],
  'patientSource': { 'strategy': 'fixed', 'fallback': 'TEST SURSA IMPLICITA', 'clinicAliases': [] },
  'procedures': []
}");
        }

        private static string PatientSourceJson(string strategy, string fallback, string aliases)
        {
            return Q(@"{
  'mappingsVersion': '1',
  'initialStatus': 'Programat',
  'referralPayers': ['CAS', 'Monitor'],
  'resources': [],
  'patientSource': { 'strategy': '" + strategy + @"', 'fallback': '" + fallback + @"', 'clinicAliases': [" + aliases + @"] },
  'procedures': []
}");
        }

        private static readonly string Full = Q(@"{
  'mappingsVersion': '1',
  'initialStatus': 'Programat',
  'referralPayers': ['CAS', 'Monitor'],
  'resources': [
    { 'branchName': 'ScanExpert Galați', 'modality': '*', 'pixelDataResource': 'TEST RESURSA GALATI' },
    { 'branchName': 'ScanExpert Galați', 'modality': 'CT', 'pixelDataResource': 'TEST RESURSA GALATI CT' },
    { 'branchName': 'ScanExpert Iași', 'modality': 'RMN', 'pixelDataResource': 'TEST RESURSA IASI RMN' },
    { 'branchName': 'ScanExpert Brașov', 'modality': '*', 'pixelDataResource': 'TODO' }
  ],
  'patientSource': {
    'strategy': 'referringDoctorClinic',
    'fallback': 'TEST SURSA IMPLICITA',
    'clinicAliases': [ { 'clinic': 'CLINICA TEST SRL', 'pixelDataSource': 'TEST CLINICA SRL GALATI' } ]
  },
  'procedures': [
    { 'productCode': 'TEST-RMN-001', 'pixelDataProcedure': 'TEST RM COLOANA CERVICALA NATIV' }
  ]
}");

        [Fact]
        public void FromJson_ReadsVersionAndInitialStatus()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);

            Assert.Equal("1", mappings.MappingsVersion);
            Assert.Equal("Programat", mappings.InitialStatus);
        }

        [Theory]
        [InlineData("CAS", true)]
        [InlineData("Monitor", true)]
        [InlineData("Contra cost", false)]
        [InlineData("Asigurator privat", false)]
        [InlineData("", false)]
        public void NeedsReferral_CasAndMonitorOnly(string payer, bool expected)
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);

            Assert.Equal(expected, mappings.NeedsReferral(payer));
        }

        [Fact]
        public void NeedsReferral_ReadsReferralPayersFromTheFile()
        {
            string json = Full.Replace("\"referralPayers\": [\"CAS\", \"Monitor\"]", "\"referralPayers\": [\"CAS\"]");
            Assert.NotEqual(Full, json);
            PixelDataMappings mappings = PixelDataMappings.FromJson(json);

            Assert.True(mappings.NeedsReferral("CAS"));
            Assert.False(mappings.NeedsReferral("Monitor"));
        }

        [Fact]
        public void ResolveResource_StarMatchesAnyModality()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);

            Assert.Equal("TEST RESURSA GALATI", mappings.ResolveResource("ScanExpert Galați", "RMN"));
            Assert.Equal("TEST RESURSA GALATI", mappings.ResolveResource("ScanExpert Galați", "Ecografie"));
        }

        [Fact]
        public void ResolveResource_ExactModalityBeatsStar_StarListedFirst()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);

            Assert.Equal("TEST RESURSA GALATI CT", mappings.ResolveResource("ScanExpert Galați", "CT"));
        }

        [Fact]
        public void ResolveResource_ExactModalityBeatsStar_ExactListedFirst()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(ResourcesJson(@"
    { 'branchName': 'ScanExpert Galați', 'modality': 'CT', 'pixelDataResource': 'TEST RESURSA GALATI CT' },
    { 'branchName': 'ScanExpert Galați', 'modality': '*', 'pixelDataResource': 'TEST RESURSA GALATI' }"));

            Assert.Equal("TEST RESURSA GALATI CT", mappings.ResolveResource("ScanExpert Galați", "CT"));
            Assert.Equal("TEST RESURSA GALATI", mappings.ResolveResource("ScanExpert Galați", "RMN"));
        }

        [Theory]
        [InlineData("  ScanExpert Galați  ", " CT ")]
        [InlineData("SCANEXPERT GALAȚI", "ct")]
        [InlineData("scanexpert galați", "Ct")]
        public void ResolveResource_TrimsAndIgnoresCase(string branchName, string modality)
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);

            Assert.Equal("TEST RESURSA GALATI CT", mappings.ResolveResource(branchName, modality));
        }

        [Fact]
        public void ResolveResource_BranchWithOtherModalityOnly_ResourceMappingMissing()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);

            var ex = Assert.Throws<BusinessRuleViolation>(() => mappings.ResolveResource("ScanExpert Iași", "CT"));

            Assert.Equal("RESOURCE_MAPPING_MISSING", ex.Code);
        }

        [Fact]
        public void ResolveResource_UnknownBranch_ResourceMappingMissing()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);

            var ex = Assert.Throws<BusinessRuleViolation>(() => mappings.ResolveResource("ScanExpert Roman", "RMN"));

            Assert.Equal("RESOURCE_MAPPING_MISSING", ex.Code);
            Assert.StartsWith("RESOURCE_MAPPING_MISSING: ", ex.Message);
        }

        [Fact]
        public void ResolveResource_TodoValue_ResourceMappingMissing()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);

            var ex = Assert.Throws<BusinessRuleViolation>(() => mappings.ResolveResource("ScanExpert Brașov", "RMN"));

            Assert.Equal("RESOURCE_MAPPING_MISSING", ex.Code);
        }

        [Fact]
        public void ResolvePatientSource_ClinicStrategy_AliasFound_UsesAlias()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);
            AppointmentItem item = Samples.Item();
            item.ReferringDoctorClinic = "CLINICA TEST SRL";

            Assert.Equal("TEST CLINICA SRL GALATI", mappings.ResolvePatientSource(item));
        }

        [Theory]
        [InlineData("ALTA CLINICA TEST SRL")]
        [InlineData("")]
        public void ResolvePatientSource_ClinicStrategy_AliasNotFound_UsesFallback(string clinic)
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);
            AppointmentItem item = Samples.Item();
            item.ReferringDoctorClinic = clinic;

            Assert.Equal("TEST SURSA IMPLICITA", mappings.ResolvePatientSource(item));
        }

        [Fact]
        public void ResolvePatientSource_ClinicStrategy_NotFoundAndFallbackTodo_PatientSourceMappingMissing()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(PatientSourceJson(
                "referringDoctorClinic", "TODO", "{ 'clinic': 'CLINICA TEST SRL', 'pixelDataSource': 'TEST CLINICA SRL GALATI' }"));
            AppointmentItem item = Samples.Item();
            item.ReferringDoctorClinic = "ALTA CLINICA TEST SRL";

            var ex = Assert.Throws<BusinessRuleViolation>(() => mappings.ResolvePatientSource(item));

            Assert.Equal("PATIENT_SOURCE_MAPPING_MISSING", ex.Code);
        }

        [Fact]
        public void ResolvePatientSource_FixedStrategy_AlwaysFallback_EvenWhenAliasMatches()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(PatientSourceJson(
                "fixed", "TEST SURSA FIXA", "{ 'clinic': 'CLINICA TEST SRL', 'pixelDataSource': 'TEST CLINICA SRL GALATI' }"));
            AppointmentItem item = Samples.Item();
            item.ReferringDoctorClinic = "CLINICA TEST SRL";

            Assert.Equal("TEST SURSA FIXA", mappings.ResolvePatientSource(item));
        }

        [Theory]
        [InlineData("TODO")]
        [InlineData("")]
        public void ResolvePatientSource_FixedStrategy_FallbackTodoOrEmpty_PatientSourceMappingMissing(string fallback)
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(PatientSourceJson("fixed", fallback, ""));

            var ex = Assert.Throws<BusinessRuleViolation>(() => mappings.ResolvePatientSource(Samples.Item()));

            Assert.Equal("PATIENT_SOURCE_MAPPING_MISSING", ex.Code);
            Assert.StartsWith("PATIENT_SOURCE_MAPPING_MISSING: ", ex.Message);
        }

        [Fact]
        public void ResolveProcedure_CodeFound_UsesPixelDataProcedure()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);

            Assert.Equal("TEST RM COLOANA CERVICALA NATIV", mappings.ResolveProcedure("TEST-RMN-001", "Alt nume din recepție"));
        }

        [Theory]
        [InlineData("TEST-NEEXISTENT")]
        [InlineData("")]
        public void ResolveProcedure_CodeNotFound_UsesProductName(string productCode)
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(Full);

            Assert.Equal("RM GENUNCHI STANG NATIV", mappings.ResolveProcedure(productCode, "RM GENUNCHI STANG NATIV"));
        }
    }
}
