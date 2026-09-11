using System;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    // contracts/examples/valid/: exemplele valide trebuie să treacă și regulile robotului.
    public class ValidExamplesTests
    {
        [Theory]
        [MemberData(nameof(Fixtures.ValidExampleFiles), MemberType = typeof(Fixtures))]
        public void ValidExample_PassesValidator_WhenCnpIsNotRequired(string relativePath)
        {
            AppointmentItem item = QueueItemParser.FromJson(Fixtures.Read(relativePath));
            var options = new ValidationOptions { RequireCnp = false };

            Assert.Empty(AppointmentValidator.Validate(item, Samples.Now, options));
        }

        [Theory]
        [MemberData(nameof(Fixtures.ValidExampleFiles), MemberType = typeof(Fixtures))]
        public void ValidExample_CnpIsEmptyOrValid(string relativePath)
        {
            AppointmentItem item = QueueItemParser.FromJson(Fixtures.Read(relativePath));

            Assert.True(item.PatientCnp == "" || CnpValidator.IsValid(item.PatientCnp), relativePath);
        }
    }
}
