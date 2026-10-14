using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClinicManagement.Application.Auth.Totp;

public static class TotpGenerator
{
    public const int DefaultStepSeconds = 30;
    public const int DefaultDigits = 6;
    public const int DefaultDriftSteps = 1;
    public const int MinSecretBytes = 20;

    public static string CreateSecret()
    {
        return Base32.Encode(RandomNumberGenerator.GetBytes(MinSecretBytes));
    }

    public static long GetTimeStep(DateTime utcNow, int stepSeconds = DefaultStepSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stepSeconds, 1);

        var unixSeconds = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();
        return unixSeconds / stepSeconds;
    }

    public static string ComputeCode(
        string base32Secret,
        long timeStep,
        int digits = DefaultDigits,
        int stepSeconds = DefaultStepSeconds,
        bool useSha256 = false)
    {
        ValidateDigits(digits);
        ArgumentOutOfRangeException.ThrowIfLessThan(stepSeconds, 1);

        var key = Base32.Decode(base32Secret);
        var counter = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);

#pragma warning disable CA5350 // RFC 6238 authenticator compatibility mandates HMAC-SHA1; it is a MAC, not a signature.
        var hash = useSha256
            ? HMACSHA256.HashData(key, counter)
            : HMACSHA1.HashData(key, counter);
#pragma warning restore CA5350

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];

        var modulo = (int)Math.Pow(10, digits);
        return (binary % modulo).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }

    public static string ComputeCodeAt(
        string base32Secret,
        DateTime utcNow,
        int digits = DefaultDigits,
        int stepSeconds = DefaultStepSeconds,
        bool useSha256 = false)
    {
        return ComputeCode(base32Secret, GetTimeStep(utcNow, stepSeconds), digits, stepSeconds, useSha256);
    }

    public static bool VerifyCode(
        string base32Secret,
        string? candidateCode,
        DateTime utcNow,
        long? lastUsedStep,
        out long matchedStep,
        int driftSteps = DefaultDriftSteps,
        int stepSeconds = DefaultStepSeconds)
    {
        matchedStep = 0;

        if (string.IsNullOrWhiteSpace(candidateCode))
        {
            return false;
        }

        var currentStep = GetTimeStep(utcNow, stepSeconds);

        for (var delta = -driftSteps; delta <= driftSteps; delta++)
        {
            var step = currentStep + delta;
            var expected = ComputeCode(base32Secret, step, DefaultDigits, stepSeconds);
            if (!FixedTimeEquals(expected, candidateCode.Trim()))
            {
                continue;
            }

            if (lastUsedStep is { } last && step <= last)
            {
                return false;
            }

            matchedStep = step;
            return true;
        }

        return false;
    }

    public static string BuildProvisioningUri(
        string issuer,
        string accountName,
        string base32Secret,
        int digits = DefaultDigits,
        int stepSeconds = DefaultStepSeconds,
        bool useSha256 = false)
    {
        var label = $"{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}";
        var parameters = new List<string>
        {
            $"secret={base32Secret}",
            $"issuer={Uri.EscapeDataString(issuer)}",
            $"algorithm={(useSha256 ? "SHA256" : "SHA1")}",
            $"digits={digits.ToString(CultureInfo.InvariantCulture)}",
            $"period={stepSeconds.ToString(CultureInfo.InvariantCulture)}"
        };

        return $"otpauth://totp/{label}?{string.Join('&', parameters)}";
    }

    private static bool FixedTimeEquals(string expected, string candidate)
    {
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var candidateBytes = Encoding.ASCII.GetBytes(candidate);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
    }

    private static void ValidateDigits(int digits)
    {
        if (digits is < 6 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(digits), digits, "digits must be between 6 and 10.");
        }
    }
}