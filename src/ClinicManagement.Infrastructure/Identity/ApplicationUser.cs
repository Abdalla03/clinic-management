using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace ClinicManagement.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    // Already ciphertext: AuthService encrypts this with the TOTP purpose before storing it, so the
    // [EncryptedColumn] converter must not be applied a second time.
    public string? TotpSecretCiphertext { get; set; }

    public DateTime? TotpEnabledAtUtc { get; set; }

    public long? LastUsedTotpStep { get; set; }

    public DateTime? AccessRevokedAtUtc { get; set; }

    public bool MfaSatisfied => TotpEnabledAtUtc is not null;

    public Doctor? Doctor { get; set; }

    public Patient? Patient { get; set; }
}