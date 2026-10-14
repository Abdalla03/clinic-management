namespace ClinicManagement.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string? DoctorPassword { get; set; }

    public string? SecretaryPassword { get; set; }

    public string? PatientPassword { get; set; }

    /// <summary>
    /// Returns the password for a demo login. A configured value wins; an explicitly empty value
    /// disables that demo login; when the setting is absent the built-in development default is used
    /// so a fresh clone works without extra configuration.
    /// </summary>
    public string? ResolvePassword(string email)
    {
        var (configured, fallback) = email switch
        {
            DemoAccounts.DoctorEmail => (DoctorPassword, DemoAccounts.DoctorDefaultPassword),
            DemoAccounts.SecretaryEmail => (SecretaryPassword, DemoAccounts.SecretaryDefaultPassword),
            DemoAccounts.PatientEmail => (PatientPassword, DemoAccounts.PatientDefaultPassword),
            _ => ((string?)null, (string?)null)
        };

        if (configured is not null)
        {
            return string.IsNullOrWhiteSpace(configured) ? null : configured;
        }

        return fallback;
    }
}
