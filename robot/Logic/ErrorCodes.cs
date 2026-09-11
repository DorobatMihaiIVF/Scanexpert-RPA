namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// Codurile de eroare ale robotului. Mesajul unei excepții începe cu codul, de exemplu "CNP_INVALID: ...".
    /// Business = problema e în date (fără retry). System = problema e în PixelData sau în mediu (retry prin coadă).
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>Business: o cheie a contractului lipsește din item sau un câmp obligatoriu este gol.</summary>
        public const string MissingField = "MISSING_FIELD";

        /// <summary>Business: o valoare este null sau are format greșit.</summary>
        public const string InvalidField = "INVALID_FIELD";

        /// <summary>Business: SchemaVersion este diferit de "1".</summary>
        public const string UnsupportedSchemaVersion = "UNSUPPORTED_SCHEMA_VERSION";

        /// <summary>Business: Operation este diferit de "create".</summary>
        public const string UnsupportedOperation = "UNSUPPORTED_OPERATION";

        /// <summary>Business: CNP-ul lipsește, iar setarea RequireCnp îl cere.</summary>
        public const string CnpRequired = "CNP_REQUIRED";

        /// <summary>Business: CNP-ul este prezent, dar invalid.</summary>
        public const string CnpInvalid = "CNP_INVALID";

        /// <summary>Business: ora programării a trecut (peste toleranța configurată).</summary>
        public const string AppointmentInPast = "APPOINTMENT_IN_PAST";

        /// <summary>Business: nu există resursă PixelData pentru sucursală și modalitate.</summary>
        public const string ResourceMappingMissing = "RESOURCE_MAPPING_MISSING";

        /// <summary>Business: „Sursa pacient” nu este configurată pentru acest item.</summary>
        public const string PatientSourceMappingMissing = "PATIENT_SOURCE_MAPPING_MISSING";

        /// <summary>Business: căutarea în PixelData a găsit mai mulți pacienți posibili.</summary>
        public const string PatientAmbiguous = "PATIENT_AMBIGUOUS";

        /// <summary>Business: slotul orar este ocupat de alt pacient.</summary>
        public const string SlotOccupied = "SLOT_OCCUPIED";

        /// <summary>Business: PixelData a respins CNP-ul (fereastra „Eroare CNP pacient!”).</summary>
        public const string PixelDataCnpRejected = "PIXELDATA_CNP_REJECTED";

        /// <summary>
        /// Business: procedura rezolvată din mapări (sau, în lipsa lor, ProductName) nu există
        /// în lista de proceduri din PixelData.
        /// </summary>
        public const string ProcedureNotFound = "PROCEDURE_NOT_FOUND";

        /// <summary>
        /// Business: ReferringDoctorName este ne-gol, dar nu apare în lista „Medic trimitator”
        /// din PixelData. Un nume gol nu este eroare.
        /// </summary>
        public const string ReferringDoctorNotFound = "REFERRING_DOCTOR_NOT_FOUND";

        /// <summary>System: PixelData nu pornește sau nu răspunde.</summary>
        public const string PixelDataUnavailable = "PIXELDATA_UNAVAILABLE";

        /// <summary>System: autentificarea în PixelData a eșuat.</summary>
        public const string PixelDataLoginFailed = "PIXELDATA_LOGIN_FAILED";

        /// <summary>System: un ecran sau un element așteptat nu a apărut la timp.</summary>
        public const string PixelDataUiTimeout = "PIXELDATA_UI_TIMEOUT";

        /// <summary>System: salvarea nu a putut fi confirmată în grila Programări.</summary>
        public const string PixelDataSaveUnconfirmed = "PIXELDATA_SAVE_UNCONFIRMED";

        /// <summary>
        /// True doar pentru codurile de business. Comparația este exactă (majusculele contează):
        /// null, un cod de sistem sau un text necunoscut dau false.
        /// </summary>
        public static bool IsBusiness(string code)
        {
            switch (code)
            {
                case MissingField:
                case InvalidField:
                case UnsupportedSchemaVersion:
                case UnsupportedOperation:
                case CnpRequired:
                case CnpInvalid:
                case AppointmentInPast:
                case ResourceMappingMissing:
                case PatientSourceMappingMissing:
                case PatientAmbiguous:
                case SlotOccupied:
                case PixelDataCnpRejected:
                case ProcedureNotFound:
                case ReferringDoctorNotFound:
                    return true;
                default:
                    return false;
            }
        }
    }
}
