using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    public class TransactionOutputTests
    {
        private static readonly DateTimeOffset ProcessedAt = new DateTimeOffset(2026, 9, 15, 8, 5, 30, TimeSpan.FromHours(3));

        private static readonly Regex Rfc3339 = new Regex(
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$",
            RegexOptions.CultureInvariant);

        [Fact]
        public void Success_HasExactlyTheOutputKeys()
        {
            Dictionary<string, object> output = TransactionOutput.Success("created", true, "TEST RESURSA", ProcessedAt);

            Assert.Equal(
                new[] { "Outcome", "OutputSchemaVersion", "PatientCreated", "PixelDataResource", "ProcessedAt" },
                output.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void Success_OutputSchemaVersionIsString1()
        {
            Dictionary<string, object> output = TransactionOutput.Success("created", true, "TEST RESURSA", ProcessedAt);

            Assert.Equal("1", Assert.IsType<string>(output["OutputSchemaVersion"]));
        }

        [Theory]
        [InlineData("created", true)]
        [InlineData("already_existed", false)]
        public void Success_KeepsOutcomeAndPatientCreatedWithTheirTypes(string outcome, bool patientCreated)
        {
            Dictionary<string, object> output = TransactionOutput.Success(outcome, patientCreated, "TEST RESURSA", ProcessedAt);

            Assert.Equal(outcome, Assert.IsType<string>(output["Outcome"]));
            Assert.Equal(patientCreated, Assert.IsType<bool>(output["PatientCreated"]));
        }

        [Fact]
        public void Success_PixelDataResourceIsString()
        {
            Dictionary<string, object> output = TransactionOutput.Success("created", false, "TEST RESURSA GALATI", ProcessedAt);

            Assert.Equal("TEST RESURSA GALATI", Assert.IsType<string>(output["PixelDataResource"]));
        }

        [Fact]
        public void Success_ProcessedAtIsRfc3339StringOfTheSameInstant()
        {
            Dictionary<string, object> output = TransactionOutput.Success("created", false, "TEST RESURSA", ProcessedAt);

            string text = Assert.IsType<string>(output["ProcessedAt"]);
            Assert.Matches(Rfc3339, text);
            Assert.Equal(ProcessedAt, DateTimeOffset.Parse(text, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Success_ProcessedAtIgnoresCurrentCulture()
        {
            // th-TH folosește calendarul budist (anul 2569): un format dependent de cultură s-ar vedea imediat.
            CultureInfo previous = CultureInfo.CurrentCulture;
            string text;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("th-TH");
                Dictionary<string, object> output = TransactionOutput.Success("created", false, "TEST RESURSA", ProcessedAt);
                text = Assert.IsType<string>(output["ProcessedAt"]);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            Assert.Matches(Rfc3339, text);
            Assert.StartsWith("2026-09-15T", text);
        }
    }
}
