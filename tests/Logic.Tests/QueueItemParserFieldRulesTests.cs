using System;
using System.Collections.Generic;
using System.Linq;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    // Regulile de format ale QueueItemParser (descrierea clasei), verificate prin FromJson.
    public class QueueItemParserFieldRulesTests
    {
        private static AppointmentItem Parse(Dictionary<string, object> content)
        {
            return QueueItemParser.FromJson(Samples.Json(content));
        }

        private static void AssertInvalidField(string key, object value)
        {
            string json = Samples.Json(Samples.ContentWith(key, value));

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromJson(json));

            Assert.Equal("INVALID_FIELD", ex.Code);
            Assert.Equal(key, ex.Field);
        }

        [Fact]
        public void FromJson_Null_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => QueueItemParser.FromJson(null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("nu este json")]
        [InlineData("{\"SchemaVersion\": \"1\",}")]
        public void FromJson_NotJson_InvalidFieldWithEmptyField(string text)
        {
            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromJson(text));

            Assert.Equal("INVALID_FIELD", ex.Code);
            Assert.Equal("", ex.Field);
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("\"text\"")]
        [InlineData("42")]
        [InlineData("null")]
        public void FromJson_RootNotAnObject_InvalidFieldWithEmptyField(string text)
        {
            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromJson(text));

            Assert.Equal("INVALID_FIELD", ex.Code);
            Assert.Equal("", ex.Field);
        }

        [Theory]
        [InlineData("SchemaVersion")]
        [InlineData("ProductName")]
        [InlineData("DurationMinutes")]
        [InlineData("ReferralPending")]
        [InlineData("ScheduledAt")]
        [InlineData("ReferralDate")]
        public void FromJson_NullValueOnKnownKey_InvalidField(string key)
        {
            AssertInvalidField(key, null);
        }

        [Fact]
        public void FromJson_ObjectValueOnKnownKey_InvalidField()
        {
            AssertInvalidField("Modality", new Dictionary<string, object> { { "Name", "RMN" } });
        }

        [Fact]
        public void FromJson_ArrayValueOnKnownKey_InvalidField()
        {
            AssertInvalidField("Notes", new[] { "prima", "a doua" });
        }

        [Fact]
        public void FromJson_ObjectOrArrayOnUnknownKey_Ignored()
        {
            Dictionary<string, object> content = Samples.Content();
            content["FutureObject"] = new Dictionary<string, object> { { "x", 1L } };
            content["FutureList"] = new[] { 1L, 2L };

            ItemAssert.Equivalent(Samples.Item(), Parse(content));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Centrala")]
        [InlineData("Aplicație")]
        [InlineData("La sediu")]
        public void Source_ContractValues_Accepted(string value)
        {
            Assert.Equal(value, Parse(Samples.ContentWith("Source", value)).Source);
        }

        [Theory]
        [InlineData("")]
        [InlineData("stanga")]
        [InlineData("dreapta")]
        [InlineData("bilateral")]
        public void Laterality_ContractValues_Accepted(string value)
        {
            Assert.Equal(value, Parse(Samples.ContentWith("Laterality", value)).Laterality);
        }

        [Theory]
        [InlineData("")]
        [InlineData("CAS")]
        [InlineData("Monitor")]
        [InlineData("Contra cost")]
        [InlineData("Asigurator privat")]
        public void Payer_ContractValues_Accepted(string value)
        {
            Assert.Equal(value, Parse(Samples.ContentWith("Payer", value)).Payer);
        }

        [Theory]
        [InlineData("Source", "Telefon")]
        [InlineData("Source", "centrala")]
        [InlineData("Source", "Aplicatie")]
        [InlineData("Laterality", "stânga")]
        [InlineData("Laterality", "Stanga")]
        [InlineData("Payer", "cas")]
        [InlineData("Payer", "Card")]
        [InlineData("Payer", "Asigurător privat")]
        public void ClosedLists_OtherValue_InvalidField(string key, string value)
        {
            AssertInvalidField(key, value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("+40700000001")]
        [InlineData("+14155550123")]
        public void PatientPhone_EmptyOrE164_Accepted(string value)
        {
            Assert.Equal(value, Parse(Samples.ContentWith("PatientPhone", value)).PatientPhone);
        }

        [Theory]
        [InlineData("0700000001")]
        [InlineData("40700000001")]
        [InlineData("+40 700 000 001")]
        [InlineData("+0700000001")]
        [InlineData("+")]
        public void PatientPhone_NotE164_InvalidField(string value)
        {
            AssertInvalidField("PatientPhone", value);
        }

        [Fact]
        public void Notes_2000Characters_Accepted()
        {
            string notes = new string('A', 2000);

            Assert.Equal(notes, Parse(Samples.ContentWith("Notes", notes)).Notes);
        }

        [Fact]
        public void Notes_2001Characters_InvalidField()
        {
            AssertInvalidField("Notes", new string('A', 2001));
        }

        [Fact]
        public void Notes_CountsCodePoints_NotUtf16Units()
        {
            // 2000 de caractere în afara BMP = 4000 de unități UTF-16; limita e pe caractere, ca în schemă.
            string notes = string.Concat(Enumerable.Repeat("\U0001D11E", 2000));

            Assert.Equal(notes, Parse(Samples.ContentWith("Notes", notes)).Notes);
        }

        [Fact]
        public void Text_IsNotTrimmed()
        {
            Assert.Equal("  RM COLOANA  ", Parse(Samples.ContentWith("ProductName", "  RM COLOANA  ")).ProductName);
        }

        [Fact]
        public void SchemaVersion_IsNotTrimmed()
        {
            string json = Samples.Json(Samples.ContentWith("SchemaVersion", " 1"));

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromJson(json));

            Assert.Equal("UNSUPPORTED_SCHEMA_VERSION", ex.Code);
        }

        [Theory]
        [InlineData(0L, 0)]
        [InlineData("0", 0)]
        [InlineData("45", 45)]
        public void DurationMinutes_WholeNonNegative_Accepted(object value, int expected)
        {
            Assert.Equal(expected, Parse(Samples.ContentWith("DurationMinutes", value)).DurationMinutes);
        }

        [Fact]
        public void DurationMinutes_JsonNumber30Point0_Accepted()
        {
            string json = Samples.Json(Samples.Content()).Replace("\"DurationMinutes\":30", "\"DurationMinutes\":30.0");
            Assert.Contains("\"DurationMinutes\":30.0", json);

            Assert.Equal(30, QueueItemParser.FromJson(json).DurationMinutes);
        }

        [Theory]
        [InlineData(-5L)]
        [InlineData(30.5)]
        [InlineData("")]
        [InlineData("-5")]
        [InlineData("30.0")]
        [InlineData(" 30")]
        public void DurationMinutes_NotAWholeNonNegativeNumber_InvalidField(object value)
        {
            AssertInvalidField("DurationMinutes", value);
        }

        [Theory]
        [InlineData("TRUE", true)]
        [InlineData("False", false)]
        public void ReferralPending_TrueFalseTextAnyCase_Accepted(string value, bool expected)
        {
            Assert.Equal(expected, Parse(Samples.ContentWith("ReferralPending", value)).ReferralPending);
        }

        [Theory]
        [InlineData("")]
        [InlineData("1")]
        [InlineData("yes")]
        [InlineData(" true")]
        [InlineData(1L)]
        public void ReferralPending_OtherValue_InvalidField(object value)
        {
            AssertInvalidField("ReferralPending", value);
        }

        [Theory]
        [InlineData("ReferralDate")]
        [InlineData("PatientBirthDate")]
        public void OptionalDate_OnlySpaces_InvalidField(string key)
        {
            AssertInvalidField(key, " ");
        }

        [Fact]
        public void ScheduledLocalDate_Empty_InvalidField()
        {
            AssertInvalidField("ScheduledLocalDate", "");
        }

        [Fact]
        public void ScheduledAt_ZuluOffset_Accepted()
        {
            AppointmentItem item = Parse(Samples.ContentWith("ScheduledAt", "2027-03-15T07:30:00Z"));

            Assert.Equal(new DateTimeOffset(2027, 3, 15, 7, 30, 0, TimeSpan.Zero), item.ScheduledAt);
        }

        [Fact]
        public void ScheduledAt_NegativeOffset_SameInstant()
        {
            AppointmentItem item = Parse(Samples.ContentWith("ScheduledAt", "2027-03-15T02:30:00-05:00"));

            Assert.Equal(new DateTimeOffset(2027, 3, 15, 7, 30, 0, TimeSpan.Zero), item.ScheduledAt);
            Assert.Equal(TimeSpan.FromHours(-5), item.ScheduledAt.Offset);
        }

        [Theory]
        [InlineData("AppointmentId", "{3f0c9a52-7d1e-4b8a-9c2d-5e6f7a8b9c01}")]
        [InlineData("AppointmentId", "3f0c9a527d1e4b8a9c2d5e6f7a8b9c01")]
        [InlineData("AppointmentId", "")]
        [InlineData("PatientId", "")]
        [InlineData("BranchId", " ")]
        public void Uuid_WrongShapeOrRequiredEmpty_InvalidField(string key, string value)
        {
            AssertInvalidField(key, value);
        }

        [Fact]
        public void PatientCnp_IsNotCheckedByTheParser()
        {
            // CNP-ul îl verifică AppointmentValidator (CNP_INVALID), nu parserul.
            Assert.Equal("12AB", Parse(Samples.ContentWith("PatientCnp", "12AB")).PatientCnp);
        }
    }
}
