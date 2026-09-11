using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    // contracts/examples/invalid/business/: trec schema, dar robotul le respinge. Tabelul e și în invalid/README.md.
    public class BusinessInvalidExamplesTests
    {
        public static IEnumerable<object[]> Cases()
        {
            yield return new object[] { "appointment-in-past.json", "APPOINTMENT_IN_PAST" };
            yield return new object[] { "branch-name-empty.json", "MISSING_FIELD" };
            yield return new object[] { "patient-cnp-empty.json", "CNP_REQUIRED" };
            yield return new object[] { "patient-cnp-invalid-month.json", "CNP_INVALID" };
            yield return new object[] { "patient-cnp-wrong-checksum.json", "CNP_INVALID" };
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void Example_ParsesButIsRejectedWithItsCode(string fileName, string expectedCode)
        {
            string json = Fixtures.Read(Path.Combine("examples", "invalid", "business", fileName));

            AppointmentItem item = QueueItemParser.FromJson(json);
            var ex = Assert.Throws<BusinessRuleViolation>(
                () => AppointmentValidator.EnsureValid(item, Samples.Now, new ValidationOptions()));

            Assert.Equal(expectedCode, ex.Code);
            Assert.True(ErrorCodes.IsBusiness(ex.Code));
        }

        [Fact]
        public void EveryBusinessExampleFile_HasAnExpectedCode()
        {
            string[] listed = Cases().Select(c => (string)c[0]).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            string[] onDisk = Fixtures.BusinessInvalidExampleFiles()
                .Select(c => Path.GetFileName((string)c[0]))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(listed, onDisk);
        }
    }
}
