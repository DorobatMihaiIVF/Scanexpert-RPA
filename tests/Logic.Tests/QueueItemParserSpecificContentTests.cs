using System;
using System.Collections.Generic;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    // In_TransactionItem.SpecificContent din Studio: valorile vin cu tipuri diferite.
    public class QueueItemParserSpecificContentTests
    {
        private sealed class TextValue
        {
            private readonly string text;

            public TextValue(string text)
            {
                this.text = text;
            }

            public override string ToString()
            {
                return text;
            }
        }

        [Fact]
        public void FromSpecificContent_JsonLikeTypes_SameAsSample()
        {
            AppointmentItem item = QueueItemParser.FromSpecificContent(Samples.Content());

            ItemAssert.Equivalent(Samples.Item(), item);
        }

        [Fact]
        public void FromSpecificContent_AgreesWithFromJson()
        {
            AppointmentItem fromDictionary = QueueItemParser.FromSpecificContent(Samples.Content());
            AppointmentItem fromJson = QueueItemParser.FromJson(Samples.Json(Samples.Content()));

            ItemAssert.Equivalent(fromJson, fromDictionary);
        }

        [Theory]
        [MemberData(nameof(Samples.ContractKeyData), MemberType = typeof(Samples))]
        public void FromSpecificContent_MissingKey_MissingField(string key)
        {
            Dictionary<string, object> content = Samples.ContentWithout(key);

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromSpecificContent(content));

            Assert.Equal("MISSING_FIELD", ex.Code);
            Assert.Equal(key, ex.Field);
        }

        [Fact]
        public void FromSpecificContent_UnknownKeysOfAnyType_AreIgnored()
        {
            Dictionary<string, object> content = Samples.Content();
            content["FutureObject"] = new object();
            content["FutureNull"] = null;
            content["FutureDate"] = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

            ItemAssert.Equivalent(Samples.Item(), QueueItemParser.FromSpecificContent(content));
        }

        [Fact]
        public void FromSpecificContent_SchemaVersion2_UnsupportedSchemaVersion()
        {
            Dictionary<string, object> content = Samples.ContentWith("SchemaVersion", "2");

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromSpecificContent(content));

            Assert.Equal("UNSUPPORTED_SCHEMA_VERSION", ex.Code);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData("true", true)]
        [InlineData("TRUE", true)]
        [InlineData("False", false)]
        [InlineData("false", false)]
        public void ReferralPending_AcceptsBoolAndTrueFalseStrings(object value, bool expected)
        {
            Dictionary<string, object> content = Samples.ContentWith("ReferralPending", value);

            Assert.Equal(expected, QueueItemParser.FromSpecificContent(content).ReferralPending);
        }

        public static IEnumerable<object[]> ThirtyAsNumbers()
        {
            yield return new object[] { 30 };
            yield return new object[] { 30L };
            yield return new object[] { (short)30 };
            yield return new object[] { (byte)30 };
            yield return new object[] { 30u };
            yield return new object[] { 30UL };
            yield return new object[] { 30.0 };
            yield return new object[] { 30m };
            yield return new object[] { "30" };
        }

        [Theory]
        [MemberData(nameof(ThirtyAsNumbers))]
        public void DurationMinutes_AcceptsNumericTypesAndString(object value)
        {
            Dictionary<string, object> content = Samples.ContentWith("DurationMinutes", value);

            Assert.Equal(30, QueueItemParser.FromSpecificContent(content).DurationMinutes);
        }

        [Theory]
        [InlineData("ScheduledAt")]
        [InlineData("CreatedAt")]
        public void DateTimeField_DateTimeOffsetValue_UsedAsIs(string key)
        {
            var value = new DateTimeOffset(2030, 3, 15, 9, 30, 0, TimeSpan.FromHours(2));
            Dictionary<string, object> content = Samples.ContentWith(key, value);

            AppointmentItem item = QueueItemParser.FromSpecificContent(content);

            DateTimeOffset actual = key == "ScheduledAt" ? item.ScheduledAt : item.CreatedAt;
            Assert.Equal(value, actual);
            Assert.Equal(value.Offset, actual.Offset);
        }

        [Theory]
        [InlineData("ScheduledAt")]
        [InlineData("CreatedAt")]
        public void DateTimeField_UtcDateTime_SameInstant(string key)
        {
            var value = new DateTime(2030, 3, 15, 7, 30, 0, DateTimeKind.Utc);
            Dictionary<string, object> content = Samples.ContentWith(key, value);

            AppointmentItem item = QueueItemParser.FromSpecificContent(content);

            DateTimeOffset actual = key == "ScheduledAt" ? item.ScheduledAt : item.CreatedAt;
            Assert.Equal(value, actual.UtcDateTime);
        }

        [Theory]
        [InlineData("ScheduledAt")]
        [InlineData("CreatedAt")]
        public void DateTimeField_LocalDateTime_SameInstant(string key)
        {
            var value = new DateTime(2030, 3, 15, 9, 30, 0, DateTimeKind.Local);
            Dictionary<string, object> content = Samples.ContentWith(key, value);

            AppointmentItem item = QueueItemParser.FromSpecificContent(content);

            DateTimeOffset actual = key == "ScheduledAt" ? item.ScheduledAt : item.CreatedAt;
            Assert.Equal(value.ToUniversalTime(), actual.UtcDateTime);
        }

        [Theory]
        [InlineData("ScheduledAt")]
        [InlineData("CreatedAt")]
        public void DateTimeField_UnspecifiedDateTime_InvalidField(string key)
        {
            var value = new DateTime(2030, 3, 15, 9, 30, 0, DateTimeKind.Unspecified);
            Dictionary<string, object> content = Samples.ContentWith(key, value);

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromSpecificContent(content));

            Assert.Equal("INVALID_FIELD", ex.Code);
            Assert.Equal(key, ex.Field);
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        public void ScheduledLocalDate_DateTimeValue_TakesTheDate(DateTimeKind kind)
        {
            var value = new DateTime(2030, 3, 15, 13, 45, 0, kind);
            Dictionary<string, object> content = Samples.ContentWith("ScheduledLocalDate", value);

            Assert.Equal(new DateTime(2030, 3, 15), QueueItemParser.FromSpecificContent(content).ScheduledLocalDate);
        }

        [Theory]
        [InlineData("ReferralDate")]
        [InlineData("PatientBirthDate")]
        public void OptionalDate_DateTimeValue_TakesTheDate(string key)
        {
            var value = new DateTime(2030, 2, 20, 18, 5, 0, DateTimeKind.Unspecified);
            Dictionary<string, object> content = Samples.ContentWith(key, value);

            AppointmentItem item = QueueItemParser.FromSpecificContent(content);

            ItemAssert.NullableDate(new DateTime(2030, 2, 20), key == "ReferralDate" ? item.ReferralDate : item.PatientBirthDate);
        }

        [Fact]
        public void AppointmentId_GuidValue_Accepted()
        {
            Guid id = Guid.Parse("3f0c9a52-7d1e-4b8a-9c2d-5e6f7a8b9c01");
            Dictionary<string, object> content = Samples.ContentWith("AppointmentId", id);

            Assert.Equal("3f0c9a52-7d1e-4b8a-9c2d-5e6f7a8b9c01", QueueItemParser.FromSpecificContent(content).AppointmentId);
        }

        [Fact]
        public void TextField_ObjectWhoseToStringIsTheValue_Accepted()
        {
            Dictionary<string, object> content = Samples.ContentWith("Modality", new TextValue("RMN"));

            Assert.Equal("RMN", QueueItemParser.FromSpecificContent(content).Modality);
        }

        [Fact]
        public void DateTimeField_ObjectWhoseToStringIsRfc3339_Accepted()
        {
            Dictionary<string, object> content = Samples.ContentWith("ScheduledAt", new TextValue("2030-03-15T09:30:00+02:00"));

            Assert.Equal(
                new DateTimeOffset(2030, 3, 15, 9, 30, 0, TimeSpan.FromHours(2)),
                QueueItemParser.FromSpecificContent(content).ScheduledAt);
        }

        [Fact]
        public void FromSpecificContent_Null_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => QueueItemParser.FromSpecificContent(null));
        }

        public static IEnumerable<object[]> NullLikeValues()
        {
            yield return new object[] { null };
            yield return new object[] { DBNull.Value };
        }

        [Theory]
        [MemberData(nameof(NullLikeValues))]
        public void KnownKey_NullOrDBNull_InvalidField(object value)
        {
            Dictionary<string, object> content = Samples.ContentWith("ProductName", value);

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromSpecificContent(content));

            Assert.Equal("INVALID_FIELD", ex.Code);
            Assert.Equal("ProductName", ex.Field);
        }

        [Theory]
        [InlineData(true, "true")]
        [InlineData(false, "false")]
        public void TextField_BoolValue_LowercaseText(bool value, string expected)
        {
            Dictionary<string, object> content = Samples.ContentWith("ProductCode", value);

            Assert.Equal(expected, QueueItemParser.FromSpecificContent(content).ProductCode);
        }

        [Fact]
        public void TextField_CharValue_Text()
        {
            Dictionary<string, object> content = Samples.ContentWith("PatientSex", 'F');

            Assert.Equal("F", QueueItemParser.FromSpecificContent(content).PatientSex);
        }

        [Fact]
        public void TextField_Numbers_InvariantCulture()
        {
            System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
            string fromLong;
            string fromDouble;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                fromLong = QueueItemParser.FromSpecificContent(Samples.ContentWith("ProductCode", 12345L)).ProductCode;
                fromDouble = QueueItemParser.FromSpecificContent(Samples.ContentWith("ReferringDoctorParafa", 1234.5)).ReferringDoctorParafa;
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previous;
            }

            Assert.Equal("12345", fromLong);
            Assert.Equal("1234.5", fromDouble);
        }

        public static IEnumerable<object[]> NonTextTimes()
        {
            yield return new object[] { new TimeSpan(9, 30, 0) };
            yield return new object[] { new DateTime(2027, 3, 15, 9, 30, 0, DateTimeKind.Unspecified) };
            yield return new object[] { 930L };
        }

        [Theory]
        [MemberData(nameof(NonTextTimes))]
        public void ScheduledLocalTime_NotHHmmText_InvalidField(object value)
        {
            Dictionary<string, object> content = Samples.ContentWith("ScheduledLocalTime", value);

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromSpecificContent(content));

            Assert.Equal("INVALID_FIELD", ex.Code);
            Assert.Equal("ScheduledLocalTime", ex.Field);
        }

        [Fact]
        public void DateOnlyField_DateTimeOffsetValue_TakesItsOwnDate()
        {
            var value = new DateTimeOffset(2027, 3, 15, 23, 30, 0, TimeSpan.FromHours(2));
            Dictionary<string, object> content = Samples.ContentWith("ScheduledLocalDate", value);

            Assert.Equal(new DateTime(2027, 3, 15), QueueItemParser.FromSpecificContent(content).ScheduledLocalDate);
        }

        [Fact]
        public void DurationMinutes_WholeDecimalWithScale_Accepted()
        {
            Dictionary<string, object> content = Samples.ContentWith("DurationMinutes", 30.0m);

            Assert.Equal(30, QueueItemParser.FromSpecificContent(content).DurationMinutes);
        }

        public static IEnumerable<object[]> BadDurations()
        {
            yield return new object[] { -1 };
            yield return new object[] { -1L };
            yield return new object[] { 30.5 };
            yield return new object[] { 30.5m };
            yield return new object[] { "" };
            yield return new object[] { "-30" };
            yield return new object[] { "3 0" };
            yield return new object[] { true };
        }

        [Theory]
        [MemberData(nameof(BadDurations))]
        public void DurationMinutes_NotAWholeNonNegativeNumber_InvalidField(object value)
        {
            Dictionary<string, object> content = Samples.ContentWith("DurationMinutes", value);

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromSpecificContent(content));

            Assert.Equal("INVALID_FIELD", ex.Code);
            Assert.Equal("DurationMinutes", ex.Field);
        }

        public static IEnumerable<object[]> BadReferralPending()
        {
            yield return new object[] { 1 };
            yield return new object[] { 1L };
            yield return new object[] { 0.0 };
            yield return new object[] { "yes" };
            yield return new object[] { "" };
        }

        [Theory]
        [MemberData(nameof(BadReferralPending))]
        public void ReferralPending_NotBoolOrTrueFalseText_InvalidField(object value)
        {
            Dictionary<string, object> content = Samples.ContentWith("ReferralPending", value);

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromSpecificContent(content));

            Assert.Equal("INVALID_FIELD", ex.Code);
            Assert.Equal("ReferralPending", ex.Field);
        }
    }
}
