namespace ClinicManagement.Infrastructure.Security;

public sealed class EncryptionOptions
{
    public const string SectionName = "Clinic:Encryption";

    public string? MasterKeyBase64 { get; set; }
}