using System;
using System.Linq;
using System.Reflection;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    public class AppointmentItemTests
    {
        [Fact]
        public void HasOnePublicPropertyPerContractKey_WithTheSameName()
        {
            string[] properties = typeof(AppointmentItem).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(Samples.ContractKeys.OrderBy(n => n, StringComparer.Ordinal).ToArray(), properties);
        }

        [Fact]
        public void PropertiesHaveTheDocumentedTypes()
        {
            Assert.Equal(typeof(DateTimeOffset), PropertyType("CreatedAt"));
            Assert.Equal(typeof(DateTimeOffset), PropertyType("ScheduledAt"));
            Assert.Equal(typeof(DateTime), PropertyType("ScheduledLocalDate"));
            Assert.Equal(typeof(TimeSpan), PropertyType("ScheduledLocalTime"));
            Assert.Equal(typeof(int), PropertyType("DurationMinutes"));
            Assert.Equal(typeof(bool), PropertyType("ReferralPending"));
            Assert.Equal(typeof(DateTime?), PropertyType("ReferralDate"));
            Assert.Equal(typeof(DateTime?), PropertyType("PatientBirthDate"));

            string[] notString = { "CreatedAt", "ScheduledAt", "ScheduledLocalDate", "ScheduledLocalTime", "DurationMinutes", "ReferralPending", "ReferralDate", "PatientBirthDate" };
            foreach (string key in Samples.ContractKeys.Where(k => !notString.Contains(k)))
            {
                Assert.Equal(typeof(string), PropertyType(key));
            }
        }

        [Fact]
        public void NewItem_TextsAreEmptyNotNull()
        {
            var item = new AppointmentItem();

            foreach (PropertyInfo property in typeof(AppointmentItem).GetProperties().Where(p => p.PropertyType == typeof(string)))
            {
                Assert.Equal("", (string)property.GetValue(item));
            }

            Assert.False(item.ReferralDate.HasValue);
            Assert.False(item.PatientBirthDate.HasValue);
            Assert.False(item.ReferralPending);
            Assert.Equal(0, item.DurationMinutes);
        }

        private static Type PropertyType(string name)
        {
            PropertyInfo property = typeof(AppointmentItem).GetProperty(name);
            Assert.NotNull(property);
            return property.PropertyType;
        }
    }
}
