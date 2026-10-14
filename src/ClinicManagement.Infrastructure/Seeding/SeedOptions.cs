namespace ClinicManagement.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string? DoctorPassword { get; set; }

    public string? SecretaryPassword { get; set; }

    public string? PatientPassword { get; set; }

    public string? ResolvePassword(string email) => email switch
    {
        DemoAccounts.DoctorEmail => DoctorPassword,
        DemoAccounts.SecretaryEmail => SecretaryPassword,
        DemoAccounts.PatientEmail => PatientPassword,
        _ => null
    };
}
