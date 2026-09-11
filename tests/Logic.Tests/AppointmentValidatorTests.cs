using System;
using System.Collections.Generic;
using System.Linq;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    public class AppointmentValidatorTests
    {
        [Fact]
        public void Options_Defaults_RequireCnpAndZeroTolerance()
        {
            var options = new ValidationOptions();

            Assert.True(options.RequireCnp);
            Assert.Equal(TimeSpan.Zero, options.PastTolerance);
        }

        [Fact]
        public void Validate_ValidItem_NoIssues()
        {
            IReadOnlyList<ValidationIssue> issues = AppointmentValidator.Validate(Samples.Item(), Samples.Now, new ValidationOptions());

            Assert.Empty(issues);
        }

        [Fact]
        public void EnsureValid_ValidItem_DoesNotThrow()
        {
            Exception ex = Record.Exception(() => AppointmentValidator.EnsureValid(Samples.Item(), Samples.Now, new ValidationOptions()));

            Assert.Null(ex);
        }

        [Theory]
        [InlineData("ProductName")]
        [InlineData("PatientFullName")]
        [InlineData("BranchName")]
        public void Validate_RequiredTextEmpty_MissingField(string field)
        {
            AppointmentItem item = Samples.Item();
            SetText(item, field, "");

            ValidationIssue issue = Assert.Single(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));

            Assert.Equal("MISSING_FIELD", issue.Code);
            Assert.Equal(field, issue.Field);
            Assert.False(string.IsNullOrEmpty(issue.Message));
        }

        [Theory]
        [InlineData("ProductName")]
        [InlineData("PatientFullName")]
        [InlineData("BranchName")]
        public void Validate_RequiredTextOnlySpaces_MissingField(string field)
        {
            AppointmentItem item = Samples.Item();
            SetText(item, field, "   ");

            ValidationIssue issue = Assert.Single(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));

            Assert.Equal("MISSING_FIELD", issue.Code);
            Assert.Equal(field, issue.Field);
        }

        [Fact]
        public void Validate_NullItemOrOptions_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => AppointmentValidator.Validate(null, Samples.Now, new ValidationOptions()));
            Assert.Throws<ArgumentNullException>(() => AppointmentValidator.Validate(Samples.Item(), Samples.Now, null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_BlankCnpAndRequired_CnpRequired(string cnp)
        {
            AppointmentItem item = Samples.Item();
            item.PatientCnp = cnp;

            ValidationIssue issue = Assert.Single(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));

            Assert.Equal("CNP_REQUIRED", issue.Code);
            Assert.Equal("PatientCnp", issue.Field);
        }

        [Fact]
        public void Validate_InvalidCnp_MessageCarriesTheValidatorReason()
        {
            AppointmentItem item = Samples.Item();
            item.PatientCnp = Samples.WrongChecksumCnp;

            ValidationIssue issue = Assert.Single(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));

            Assert.Contains(CnpValidator.ReasonChecksum, issue.Message);
            Assert.DoesNotContain(Samples.WrongChecksumCnp, issue.Message);
        }

        [Fact]
        public void Validate_EmptyCnpAndNotRequired_NoIssues()
        {
            AppointmentItem item = Samples.Item();
            item.PatientCnp = "";
            var options = new ValidationOptions { RequireCnp = false };

            Assert.Empty(AppointmentValidator.Validate(item, Samples.Now, options));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Validate_PresentInvalidCnp_CnpInvalidWhateverRequireCnp(bool requireCnp)
        {
            AppointmentItem item = Samples.Item();
            item.PatientCnp = Samples.WrongChecksumCnp;
            var options = new ValidationOptions { RequireCnp = requireCnp };

            ValidationIssue issue = Assert.Single(AppointmentValidator.Validate(item, Samples.Now, options));

            Assert.Equal("CNP_INVALID", issue.Code);
            Assert.Equal("PatientCnp", issue.Field);
        }

        [Fact]
        public void Validate_ScheduledBeforeNow_AppointmentInPast()
        {
            AppointmentItem item = Samples.Item();
            item.ScheduledAt = Samples.Now.AddMinutes(-1);

            ValidationIssue issue = Assert.Single(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));

            Assert.Equal("APPOINTMENT_IN_PAST", issue.Code);
            Assert.Equal("ScheduledAt", issue.Field);
        }

        [Fact]
        public void Validate_ScheduledAfterNow_NoIssues()
        {
            AppointmentItem item = Samples.Item();
            item.ScheduledAt = Samples.Now.AddMinutes(1);

            Assert.Empty(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));
        }

        [Fact]
        public void Validate_PastButWithinTolerance_NoIssues()
        {
            AppointmentItem item = Samples.Item();
            item.ScheduledAt = Samples.Now.AddMinutes(-10);
            var options = new ValidationOptions { PastTolerance = TimeSpan.FromMinutes(30) };

            Assert.Empty(AppointmentValidator.Validate(item, Samples.Now, options));
        }

        [Fact]
        public void Validate_ExactlyAtNowMinusTolerance_NoIssues()
        {
            // Regula e „strict mai mic decât now - PastTolerance”.
            AppointmentItem item = Samples.Item();
            item.ScheduledAt = Samples.Now.AddMinutes(-30);
            var options = new ValidationOptions { PastTolerance = TimeSpan.FromMinutes(30) };

            Assert.Empty(AppointmentValidator.Validate(item, Samples.Now, options));
        }

        [Fact]
        public void Validate_ExactlyAtNowWithZeroTolerance_NoIssues()
        {
            AppointmentItem item = Samples.Item();
            item.ScheduledAt = Samples.Now;

            Assert.Empty(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));
        }

        [Fact]
        public void Validate_PastBeyondTolerance_AppointmentInPast()
        {
            AppointmentItem item = Samples.Item();
            item.ScheduledAt = Samples.Now.AddMinutes(-31);
            var options = new ValidationOptions { PastTolerance = TimeSpan.FromMinutes(30) };

            ValidationIssue issue = Assert.Single(AppointmentValidator.Validate(item, Samples.Now, options));

            Assert.Equal("APPOINTMENT_IN_PAST", issue.Code);
        }

        [Fact]
        public void Validate_ComparesInstants_NotClockNumbers()
        {
            // 09:30+02:00 = 07:30Z, deci e înainte de 08:00Z, deși 09:30 > 08:00.
            AppointmentItem item = Samples.Item();
            item.ScheduledAt = new DateTimeOffset(2030, 3, 15, 9, 30, 0, TimeSpan.FromHours(2));
            var now = new DateTimeOffset(2030, 3, 15, 8, 0, 0, TimeSpan.Zero);

            ValidationIssue issue = Assert.Single(AppointmentValidator.Validate(item, now, new ValidationOptions()));

            Assert.Equal("APPOINTMENT_IN_PAST", issue.Code);
        }

        [Fact]
        public void Validate_PrivateInsurerWithoutInsurer_MissingInsurer()
        {
            AppointmentItem item = Samples.Item();
            item.Payer = "Asigurator privat";
            item.Insurer = "";

            ValidationIssue issue = Assert.Single(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));

            Assert.Equal("MISSING_FIELD", issue.Code);
            Assert.Equal("Insurer", issue.Field);
        }

        [Fact]
        public void Validate_PrivateInsurerWithOnlySpacesInsurer_MissingInsurer()
        {
            AppointmentItem item = Samples.Item();
            item.Payer = "Asigurator privat";
            item.Insurer = "  ";

            ValidationIssue issue = Assert.Single(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));

            Assert.Equal("Insurer", issue.Field);
        }

        [Fact]
        public void Validate_PayerComparisonIsExact()
        {
            // Payer vine din enum-ul contractului; altă scriere nu e tratată ca asigurător privat.
            AppointmentItem item = Samples.Item();
            item.Payer = "asigurator privat";
            item.Insurer = "";

            Assert.Empty(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));
        }

        [Fact]
        public void Validate_PrivateInsurerWithInsurer_NoIssues()
        {
            AppointmentItem item = Samples.Item();
            item.Payer = "Asigurator privat";
            item.Insurer = "TEST ASIGURARI SA";

            Assert.Empty(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("CAS")]
        [InlineData("Monitor")]
        [InlineData("Contra cost")]
        public void Validate_OtherPayerWithoutInsurer_NoIssues(string payer)
        {
            AppointmentItem item = Samples.Item();
            item.Payer = payer;
            item.Insurer = "";

            Assert.Empty(AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions()));
        }

        [Fact]
        public void Validate_EveryRuleBroken_IssuesInRuleOrder()
        {
            AppointmentItem item = Samples.Item();
            item.ProductName = "";
            item.PatientFullName = "";
            item.BranchName = "";
            item.PatientCnp = "";
            item.ScheduledAt = Samples.Now.AddDays(-1);
            item.Payer = "Asigurator privat";
            item.Insurer = "";

            IReadOnlyList<ValidationIssue> issues = AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions());

            Assert.Equal(
                new[] { "MISSING_FIELD", "MISSING_FIELD", "MISSING_FIELD", "CNP_REQUIRED", "APPOINTMENT_IN_PAST", "MISSING_FIELD" },
                issues.Select(i => i.Code).ToArray());
            Assert.Equal(
                new[] { "ProductName", "PatientFullName", "BranchName", "PatientCnp", "ScheduledAt", "Insurer" },
                issues.Select(i => i.Field).ToArray());
        }

        [Fact]
        public void Validate_InvalidCnpAndInPast_CnpComesFirst()
        {
            AppointmentItem item = Samples.Item();
            item.PatientCnp = Samples.WrongChecksumCnp;
            item.ScheduledAt = Samples.Now.AddHours(-2);

            IReadOnlyList<ValidationIssue> issues = AppointmentValidator.Validate(item, Samples.Now, new ValidationOptions());

            Assert.Equal(new[] { "CNP_INVALID", "APPOINTMENT_IN_PAST" }, issues.Select(i => i.Code).ToArray());
        }

        [Fact]
        public void EnsureValid_SeveralIssues_ThrowsTheFirst()
        {
            AppointmentItem item = Samples.Item();
            item.BranchName = "";
            item.PatientCnp = Samples.WrongChecksumCnp;
            var options = new ValidationOptions();
            ValidationIssue first = AppointmentValidator.Validate(item, Samples.Now, options)[0];

            var ex = Assert.Throws<BusinessRuleViolation>(() => AppointmentValidator.EnsureValid(item, Samples.Now, options));

            Assert.Equal("MISSING_FIELD", ex.Code);
            Assert.Equal("BranchName", ex.Field);
            Assert.Equal(first.Code + ": " + first.Message, ex.Message);
        }

        [Fact]
        public void EnsureValid_InPast_ThrowsAppointmentInPast()
        {
            AppointmentItem item = Samples.Item();
            item.ScheduledAt = Samples.Now.AddMinutes(-5);

            var ex = Assert.Throws<BusinessRuleViolation>(() => AppointmentValidator.EnsureValid(item, Samples.Now, new ValidationOptions()));

            Assert.Equal("APPOINTMENT_IN_PAST", ex.Code);
            Assert.StartsWith("APPOINTMENT_IN_PAST: ", ex.Message);
        }

        private static void SetText(AppointmentItem item, string field, string value)
        {
            switch (field)
            {
                case "ProductName":
                    item.ProductName = value;
                    break;
                case "PatientFullName":
                    item.PatientFullName = value;
                    break;
                case "BranchName":
                    item.BranchName = value;
                    break;
                default:
                    throw new ArgumentException("Câmp netratat în test: " + field);
            }
        }
    }
}
