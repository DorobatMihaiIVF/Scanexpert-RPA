using System;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    public static class ItemAssert
    {
        // Compară toate cele 33 de proprietăți din contract. Datele cu offset se compară ca momente în timp.
        public static void Equivalent(AppointmentItem expected, AppointmentItem actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
            Assert.Equal(expected.Operation, actual.Operation);
            Assert.Equal(expected.AppointmentId, actual.AppointmentId);
            Assert.Equal(expected.CreatedAt, actual.CreatedAt);
            Assert.Equal(expected.Source, actual.Source);
            Assert.Equal(expected.ScheduledAt, actual.ScheduledAt);
            Assert.Equal(expected.ScheduledLocalDate, actual.ScheduledLocalDate);
            Assert.Equal(expected.ScheduledLocalTime, actual.ScheduledLocalTime);
            Assert.Equal(expected.DurationMinutes, actual.DurationMinutes);
            Assert.Equal(expected.BranchId, actual.BranchId);
            Assert.Equal(expected.BranchName, actual.BranchName);
            Assert.Equal(expected.Modality, actual.Modality);
            Assert.Equal(expected.ProductId, actual.ProductId);
            Assert.Equal(expected.ProductCode, actual.ProductCode);
            Assert.Equal(expected.ProductName, actual.ProductName);
            Assert.Equal(expected.Laterality, actual.Laterality);
            Assert.Equal(expected.Payer, actual.Payer);
            Assert.Equal(expected.Insurer, actual.Insurer);
            NullableDate(expected.ReferralDate, actual.ReferralDate);
            Assert.Equal(expected.ReferralPending, actual.ReferralPending);
            Assert.Equal(expected.ReferralNumber, actual.ReferralNumber);
            Assert.Equal(expected.ReferringDoctorName, actual.ReferringDoctorName);
            Assert.Equal(expected.ReferringDoctorClinic, actual.ReferringDoctorClinic);
            Assert.Equal(expected.ReferringDoctorParafa, actual.ReferringDoctorParafa);
            Assert.Equal(expected.PatientId, actual.PatientId);
            Assert.Equal(expected.PatientFullName, actual.PatientFullName);
            Assert.Equal(expected.PatientLastName, actual.PatientLastName);
            Assert.Equal(expected.PatientFirstName, actual.PatientFirstName);
            Assert.Equal(expected.PatientCnp, actual.PatientCnp);
            Assert.Equal(expected.PatientPhone, actual.PatientPhone);
            NullableDate(expected.PatientBirthDate, actual.PatientBirthDate);
            Assert.Equal(expected.PatientSex, actual.PatientSex);
            Assert.Equal(expected.Notes, actual.Notes);
        }

        public static void NullableDate(DateTime? expected, DateTime? actual)
        {
            Assert.Equal(expected.HasValue, actual.HasValue);
            Assert.Equal(expected.GetValueOrDefault(), actual.GetValueOrDefault());
        }
    }
}
