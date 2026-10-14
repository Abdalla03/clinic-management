namespace ClinicManagement.Infrastructure.Seeding;

/// <summary>
/// The development demo logins. They are created only when the host runs in Development or Testing,
/// the passwords come from configuration (<c>Seed:*</c>) and the whole set is documented in the
/// README so anyone running the sample can sign in as a doctor, a secretary and a patient.
/// </summary>
public static class DemoAccounts
{
    public const string DoctorEmail = "doctor@clinic.local";
    public const string SecretaryEmail = "secretary@clinic.local";
    public const string PatientEmail = "patient@clinic.local";

    public const string DoctorFullName = "Dr. Dana Farouk";
    public const string SecretaryFullName = "Salma Hassan";
    public const string PatientFullName = "Nour Ibrahim";

    public const string DoctorSpecialty = "Cardiology";
    public const string DoctorLicenseNumber = "LIC-DEMO-0001";
    public const string PatientMedicalRecordNumber = "MRN-DEMO-0001";
}
