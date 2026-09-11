using System;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    public class BusinessRuleViolationTests
    {
        [Fact]
        public void Message_IsCodeColonMessage()
        {
            var ex = new BusinessRuleViolation("CNP_INVALID", "PatientCnp", "cifra de control nu se potrivește");

            Assert.Equal("CNP_INVALID: cifra de control nu se potrivește", ex.Message);
        }

        [Fact]
        public void Properties_KeepCodeAndField()
        {
            var ex = new BusinessRuleViolation("MISSING_FIELD", "BranchName", "lipsește");

            Assert.Equal("MISSING_FIELD", ex.Code);
            Assert.Equal("BranchName", ex.Field);
        }

        [Fact]
        public void IsAnException_SoXamlCanCatchIt()
        {
            Exception caught = null;
            try
            {
                throw new BusinessRuleViolation("APPOINTMENT_IN_PAST", "ScheduledAt", "ora a trecut");
            }
            catch (Exception e)
            {
                caught = e;
            }

            var violation = Assert.IsType<BusinessRuleViolation>(caught);
            Assert.StartsWith("APPOINTMENT_IN_PAST: ", violation.Message);
        }

        [Fact]
        public void NullArguments_BecomeEmpty()
        {
            var ex = new BusinessRuleViolation(null, null, null);

            Assert.Equal("", ex.Code);
            Assert.Equal("", ex.Field);
            Assert.Equal(": ", ex.Message);
        }

        [Fact]
        public void EmptyField_IsAllowed()
        {
            var ex = new BusinessRuleViolation("INVALID_FIELD", "", "JSON invalid");

            Assert.Equal("", ex.Field);
            Assert.Equal("INVALID_FIELD: JSON invalid", ex.Message);
        }

        [Theory]
        [InlineData("RESOURCE_MAPPING_MISSING", "BranchName", "fără resursă", "RESOURCE_MAPPING_MISSING: fără resursă")]
        [InlineData("INVALID_FIELD", "ScheduledLocalTime", "format HH:MM", "INVALID_FIELD: format HH:MM")]
        public void Message_FormatHoldsForAnyCode(string code, string field, string message, string expected)
        {
            var ex = new BusinessRuleViolation(code, field, message);

            Assert.Equal(expected, ex.Message);
        }
    }
}
