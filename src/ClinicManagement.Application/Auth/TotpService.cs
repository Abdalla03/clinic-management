using ClinicManagement.Application.Abstractions;
using ClinicManagement.Application.Auth.Totp;
using ClinicManagement.Application.Common;

namespace ClinicManagement.Application.Auth;

public sealed class TotpService : ITotpService
{
    public string CreateSecret() => TotpGenerator.CreateSecret();

    public string GetProvisioningUri(string issuer, string accountName, string base32Secret) =>
        TotpGenerator.BuildProvisioningUri(issuer, accountName, base32Secret);

    public Result<bool> VerifyCode(
        string base32Secret,
        string? candidateCode,
        DateTime utcNow,
        long? lastUsedStep,
        out long matchedStep)
    {
        if (string.IsNullOrWhiteSpace(base32Secret))
        {
            matchedStep = 0;
            return Result<bool>.Failure(AuthErrorCodes.MfaNotEnabled, "No MFA secret is configured for this account.");
        }

        if (string.IsNullOrWhiteSpace(candidateCode))
        {
            matchedStep = 0;
            return Result<bool>.Failure(AuthErrorCodes.MfaInvalidCode, "The verification code is not valid.");
        }

        var normalized = candidateCode.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);

        if (normalized.Length != TotpGenerator.DefaultDigits || !normalized.All(char.IsAsciiDigit))
        {
            matchedStep = 0;
            return Result<bool>.Failure(AuthErrorCodes.MfaInvalidCode, "The verification code is not valid.");
        }

        var verified = TotpGenerator.VerifyCode(base32Secret, normalized, utcNow, lastUsedStep, out matchedStep);
        return verified
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(AuthErrorCodes.MfaInvalidCode, "The verification code is not valid.");
    }
}