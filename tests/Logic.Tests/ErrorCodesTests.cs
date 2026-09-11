using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    public class ErrorCodesTests
    {
        [Fact]
        public void Constants_HaveTheDocumentedValues()
        {
            Assert.Equal("MISSING_FIELD", ErrorCodes.MissingField);
            Assert.Equal("INVALID_FIELD", ErrorCodes.InvalidField);
            Assert.Equal("UNSUPPORTED_SCHEMA_VERSION", ErrorCodes.UnsupportedSchemaVersion);
            Assert.Equal("UNSUPPORTED_OPERATION", ErrorCodes.UnsupportedOperation);
            Assert.Equal("CNP_REQUIRED", ErrorCodes.CnpRequired);
            Assert.Equal("CNP_INVALID", ErrorCodes.CnpInvalid);
            Assert.Equal("APPOINTMENT_IN_PAST", ErrorCodes.AppointmentInPast);
            Assert.Equal("RESOURCE_MAPPING_MISSING", ErrorCodes.ResourceMappingMissing);
            Assert.Equal("PATIENT_SOURCE_MAPPING_MISSING", ErrorCodes.PatientSourceMappingMissing);
            Assert.Equal("PATIENT_AMBIGUOUS", ErrorCodes.PatientAmbiguous);
            Assert.Equal("SLOT_OCCUPIED", ErrorCodes.SlotOccupied);
            Assert.Equal("PIXELDATA_CNP_REJECTED", ErrorCodes.PixelDataCnpRejected);
            Assert.Equal("PROCEDURE_NOT_FOUND", ErrorCodes.ProcedureNotFound);
            Assert.Equal("REFERRING_DOCTOR_NOT_FOUND", ErrorCodes.ReferringDoctorNotFound);
            Assert.Equal("PIXELDATA_UNAVAILABLE", ErrorCodes.PixelDataUnavailable);
            Assert.Equal("PIXELDATA_LOGIN_FAILED", ErrorCodes.PixelDataLoginFailed);
            Assert.Equal("PIXELDATA_UI_TIMEOUT", ErrorCodes.PixelDataUiTimeout);
            Assert.Equal("PIXELDATA_SAVE_UNCONFIRMED", ErrorCodes.PixelDataSaveUnconfirmed);
        }

        [Theory]
        [InlineData("MISSING_FIELD")]
        [InlineData("INVALID_FIELD")]
        [InlineData("UNSUPPORTED_SCHEMA_VERSION")]
        [InlineData("UNSUPPORTED_OPERATION")]
        [InlineData("CNP_REQUIRED")]
        [InlineData("CNP_INVALID")]
        [InlineData("APPOINTMENT_IN_PAST")]
        [InlineData("RESOURCE_MAPPING_MISSING")]
        [InlineData("PATIENT_SOURCE_MAPPING_MISSING")]
        [InlineData("PATIENT_AMBIGUOUS")]
        [InlineData("SLOT_OCCUPIED")]
        [InlineData("PIXELDATA_CNP_REJECTED")]
        [InlineData("PROCEDURE_NOT_FOUND")]
        [InlineData("REFERRING_DOCTOR_NOT_FOUND")]
        public void IsBusiness_TrueForBusinessCodes(string code)
        {
            Assert.True(ErrorCodes.IsBusiness(code));
        }

        [Theory]
        [InlineData("PIXELDATA_UNAVAILABLE")]
        [InlineData("PIXELDATA_LOGIN_FAILED")]
        [InlineData("PIXELDATA_UI_TIMEOUT")]
        [InlineData("PIXELDATA_SAVE_UNCONFIRMED")]
        public void IsBusiness_FalseForSystemCodes(string code)
        {
            Assert.False(ErrorCodes.IsBusiness(code));
        }

        [Theory]
        [InlineData((string)null)]
        [InlineData("")]
        [InlineData("cnp_invalid")]
        [InlineData(" CNP_INVALID")]
        [InlineData("UNKNOWN_CODE")]
        public void IsBusiness_FalseForNullUnknownOrDifferentSpelling(string code)
        {
            Assert.False(ErrorCodes.IsBusiness(code));
        }
    }
}
