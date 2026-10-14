using ClinicManagement.Application.Common;

namespace ClinicManagement.Application.Abstractions;

public interface ITotpService
{
    string CreateSecret();

    string GetProvisioningUri(string issuer, string accountName, string base32Secret);

    Result<bool> VerifyCode(string base32Secret, string? candidateCode, DateTime utcNow, long? lastUsedStep, out long matchedStep);
}