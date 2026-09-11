using System;
using System.Collections.Generic;
using System.Reflection;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    public class QueueItemParserJsonTests
    {
        [Theory]
        [MemberData(nameof(Fixtures.ValidExampleFiles), MemberType = typeof(Fixtures))]
        public void FromJson_EveryValidExample_Parses(string relativePath)
        {
            AppointmentItem item = QueueItemParser.FromJson(Fixtures.Read(relativePath));

            Assert.Equal("1", item.SchemaVersion);
            Assert.Equal("create", item.Operation);
            Assert.Equal(TimeSpan.Zero, item.ScheduledLocalDate.TimeOfDay);
            foreach (PropertyInfo property in typeof(AppointmentItem).GetProperties())
            {
                if (property.PropertyType == typeof(string))
                {
                    Assert.NotNull(property.GetValue(item));
                }
            }
        }

        [Fact]
        public void FromJson_SampleContent_MapsEveryKeyWithItsType()
        {
            AppointmentItem item = QueueItemParser.FromJson(Samples.Json(Samples.Content()));

            ItemAssert.Equivalent(Samples.Item(), item);
        }

        [Theory]
        [MemberData(nameof(Samples.ContractKeyData), MemberType = typeof(Samples))]
        public void FromJson_MissingKey_MissingField(string key)
        {
            string json = Samples.Json(Samples.ContentWithout(key));

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromJson(json));

            Assert.Equal("MISSING_FIELD", ex.Code);
            Assert.Equal(key, ex.Field);
            Assert.StartsWith("MISSING_FIELD: ", ex.Message);
        }

        [Theory]
        [InlineData("2")]
        [InlineData("")]
        [InlineData("1.0")]
        [InlineData("v1")]
        public void FromJson_SchemaVersionNot1_UnsupportedSchemaVersion(string version)
        {
            string json = Samples.Json(Samples.ContentWith("SchemaVersion", version));

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromJson(json));

            Assert.Equal("UNSUPPORTED_SCHEMA_VERSION", ex.Code);
        }

        [Theory]
        [InlineData("cancel")]
        [InlineData("update")]
        [InlineData("")]
        public void FromJson_OperationNotCreate_UnsupportedOperation(string operation)
        {
            string json = Samples.Json(Samples.ContentWith("Operation", operation));

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromJson(json));

            Assert.Equal("UNSUPPORTED_OPERATION", ex.Code);
        }

        [Fact]
        public void FromJson_UnknownKeys_AreIgnored()
        {
            Dictionary<string, object> content = Samples.Content();
            content["FutureKey"] = "valoare dintr-o versiune viitoare";
            content["FutureNumber"] = 42L;
            content["FutureFlag"] = true;

            AppointmentItem item = QueueItemParser.FromJson(Samples.Json(content));

            ItemAssert.Equivalent(Samples.Item(), item);
        }

        [Theory]
        [InlineData("ReferralDate")]
        [InlineData("PatientBirthDate")]
        public void FromJson_EmptyOptionalDate_IsNull(string key)
        {
            string json = Samples.Json(Samples.ContentWith(key, ""));

            AppointmentItem item = QueueItemParser.FromJson(json);

            DateTime? value = key == "ReferralDate" ? item.ReferralDate : item.PatientBirthDate;
            Assert.False(value.HasValue);
        }

        [Theory]
        [InlineData("BranchId")]
        [InlineData("ProductId")]
        public void FromJson_EmptyOptionalUuid_IsEmptyString(string key)
        {
            string json = Samples.Json(Samples.ContentWith(key, ""));

            AppointmentItem item = QueueItemParser.FromJson(json);

            Assert.Equal("", key == "BranchId" ? item.BranchId : item.ProductId);
        }

        [Theory]
        [InlineData("ProductName")]
        [InlineData("PatientFullName")]
        [InlineData("BranchName")]
        [InlineData("PatientCnp")]
        public void FromJson_EmptyTextCheckedByValidator_StillParses(string key)
        {
            // Golurile astea sunt treaba lui AppointmentValidator, nu a parserului.
            string json = Samples.Json(Samples.ContentWith(key, ""));

            AppointmentItem item = QueueItemParser.FromJson(json);

            Assert.NotNull(item);
        }

        [Theory]
        [InlineData("AppointmentId", "not-a-uuid")]
        [InlineData("PatientId", "12345")]
        [InlineData("BranchId", "not-a-uuid")]
        [InlineData("ProductId", "xyz")]
        [InlineData("CreatedAt", "ieri")]
        [InlineData("ScheduledAt", "2030-03-15T09:30:00")]
        [InlineData("ScheduledAt", "2030-03-15 09:30")]
        [InlineData("ScheduledAt", "15.03.2030 09:30")]
        [InlineData("ScheduledLocalDate", "15.03.2030")]
        [InlineData("ScheduledLocalDate", "2030-02-30")]
        [InlineData("ScheduledLocalTime", "25:00")]
        [InlineData("ScheduledLocalTime", "09:60")]
        [InlineData("ScheduledLocalTime", "abc")]
        [InlineData("DurationMinutes", "abc")]
        [InlineData("ReferralDate", "20.02.2030")]
        [InlineData("PatientBirthDate", "1985-13-12")]
        [InlineData("ReferralPending", "maybe")]
        public void FromJson_BadFormat_InvalidField(string key, string value)
        {
            string json = Samples.Json(Samples.ContentWith(key, value));

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromJson(json));

            Assert.Equal("INVALID_FIELD", ex.Code);
            Assert.Equal(key, ex.Field);
            Assert.StartsWith("INVALID_FIELD: ", ex.Message);
        }

        [Fact]
        public void FromJson_ReferralPendingTrue_Parsed()
        {
            string json = Samples.Json(Samples.ContentWith("ReferralPending", true));

            Assert.True(QueueItemParser.FromJson(json).ReferralPending);
        }

        [Fact]
        public void FromJson_SchemaVersionCheckedBeforeOtherKeys()
        {
            // Un item dintr-o versiune viitoare poate avea alte chei; codul corect e UNSUPPORTED_SCHEMA_VERSION.
            Dictionary<string, object> content = Samples.ContentWith("SchemaVersion", "2");
            content.Remove("PatientFullName");

            var ex = Assert.Throws<BusinessRuleViolation>(() => QueueItemParser.FromJson(Samples.Json(content)));

            Assert.Equal("UNSUPPORTED_SCHEMA_VERSION", ex.Code);
        }
    }
}
