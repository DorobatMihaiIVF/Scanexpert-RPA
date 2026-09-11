using System;
using System.Collections.Generic;
using System.Globalization;

namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// Regulile de business verificate înainte ca robotul să atingă PixelData.
    /// </summary>
    /// <remarks>
    /// Reguli, în ordine:
    /// 1. ProductName, PatientFullName, BranchName goale (sau doar spații) ⇒ MISSING_FIELD, câte una pentru fiecare.
    /// 2. CNP gol și RequireCnp ⇒ CNP_REQUIRED; CNP ne-gol și invalid (CnpValidator) ⇒ CNP_INVALID.
    /// 3. ScheduledAt strict mai mic decât now - PastTolerance ⇒ APPOINTMENT_IN_PAST.
    /// 4. Payer = "Asigurator privat" și Insurer gol ⇒ MISSING_FIELD (Insurer).
    /// </remarks>
    public static class AppointmentValidator
    {
        private const string PrivateInsurerPayer = "Asigurator privat";

        /// <summary>Returnează toate problemele, în ordinea regulilor; lista e goală dacă itemul e valid.</summary>
        /// <param name="item">Itemul parsat.</param>
        /// <param name="now">Momentul procesării (în XAML: DateTimeOffset.Now).</param>
        /// <param name="options">Opțiunile din Config.</param>
        public static IReadOnlyList<ValidationIssue> Validate(AppointmentItem item, DateTimeOffset now, ValidationOptions options)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            List<ValidationIssue> issues = new List<ValidationIssue>();
            AddIfBlank(issues, item.ProductName, "ProductName");
            AddIfBlank(issues, item.PatientFullName, "PatientFullName");
            AddIfBlank(issues, item.BranchName, "BranchName");

            if (string.IsNullOrWhiteSpace(item.PatientCnp))
            {
                if (options.RequireCnp)
                {
                    issues.Add(new ValidationIssue(
                        ErrorCodes.CnpRequired,
                        "PatientCnp",
                        "CNP-ul pacientului lipsește, iar RequireCnp este activ"));
                }
            }
            else
            {
                CnpCheckResult cnp = CnpValidator.Check(item.PatientCnp);
                if (!cnp.IsValid)
                {
                    issues.Add(new ValidationIssue(ErrorCodes.CnpInvalid, "PatientCnp", "CNP invalid: " + cnp.Reason));
                }
            }

            if (item.ScheduledAt < now - options.PastTolerance)
            {
                string minutes = options.PastTolerance.TotalMinutes.ToString(CultureInfo.InvariantCulture);
                issues.Add(new ValidationIssue(
                    ErrorCodes.AppointmentInPast,
                    "ScheduledAt",
                    "ora programării a trecut (toleranță " + minutes + " minute)"));
            }

            if (string.Equals(item.Payer, PrivateInsurerPayer, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(item.Insurer))
            {
                issues.Add(new ValidationIssue(
                    ErrorCodes.MissingField,
                    "Insurer",
                    "Payer este Asigurator privat, dar Insurer este gol"));
            }

            return issues.AsReadOnly();
        }

        /// <summary>Aruncă <see cref="BusinessRuleViolation"/> pentru prima problemă găsită de Validate.</summary>
        public static void EnsureValid(AppointmentItem item, DateTimeOffset now, ValidationOptions options)
        {
            IReadOnlyList<ValidationIssue> issues = Validate(item, now, options);
            if (issues.Count > 0)
            {
                ValidationIssue first = issues[0];
                throw new BusinessRuleViolation(first.Code, first.Field, first.Message);
            }
        }

        private static void AddIfBlank(List<ValidationIssue> issues, string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add(new ValidationIssue(ErrorCodes.MissingField, field, "câmpul " + field + " este gol"));
            }
        }
    }
}
