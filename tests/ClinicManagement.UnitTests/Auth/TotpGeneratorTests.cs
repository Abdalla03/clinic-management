using ClinicManagement.Application.Auth.Totp;
using Xunit;

namespace ClinicManagement.UnitTests.Auth;

public sealed class TotpGeneratorTests
{
    private const string RfcSha1Seed = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "94287082")]
    [InlineData(1111111109L, "07081804")]
    [InlineData(1111111111L, "14050471")]
    [InlineData(1234567890L, "89005924")]
    [InlineData(2000000000L, "69279037")]
    [InlineData(20000000000L, "65353130")]
    public void ComputeCode_WithSha1_MatchesRfc6238TestVectors(long unixSeconds, string expected)
    {
        var timeStep = TotpGenerator.GetTimeStep(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime);

        var code = TotpGenerator.ComputeCode(RfcSha1Seed, timeStep, digits: 8);

        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1234567890L, "005924")]
    [InlineData(20000000000L, "353130")]
    public void ComputeCode_WithSixDigits_MatchesTheTruncatedRfcVector(long unixSeconds, string expected)
    {
        var timeStep = TotpGenerator.GetTimeStep(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime);

        var code = TotpGenerator.ComputeCode(RfcSha1Seed, timeStep, digits: 6);

        Assert.Equal(expected, code);
    }

    [Fact]
    public void ComputeCode_WithSha256_MatchesRfc6238TestVector()
    {
        var seed = Base32.Encode("12345678901234567890123456789012"u8);

        var code = TotpGenerator.ComputeCode(seed, timeStep: 1, digits: 8, useSha256: true);

        Assert.Equal("46119246", code);
    }

    [Fact]
    public void CreateSecret_ProducesADistinct160BitSecret()
    {
        var first = TotpGenerator.CreateSecret();
        var second = TotpGenerator.CreateSecret();

        Assert.NotEqual(first, second);
        Assert.Equal(32, first.Length);
        Assert.Equal(TotpGenerator.MinSecretBytes, Base32.Decode(first).Length);
    }

    [Fact]
    public void VerifyCode_AcceptsTheCurrentStep()
    {
        var secret = TotpGenerator.CreateSecret();
        var now = new DateTime(2026, 3, 14, 9, 30, 0, DateTimeKind.Utc);
        var code = TotpGenerator.ComputeCodeAt(secret, now);

        var verified = TotpGenerator.VerifyCode(secret, code, now, lastUsedStep: null, out var matchedStep);

        Assert.True(verified);
        Assert.Equal(TotpGenerator.GetTimeStep(now), matchedStep);
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(30)]
    public void VerifyCode_AcceptsOneStepOfClockDrift(int driftSeconds)
    {
        var secret = TotpGenerator.CreateSecret();
        var now = new DateTime(2026, 3, 14, 9, 30, 0, DateTimeKind.Utc);
        var drifted = now.AddSeconds(driftSeconds);
        var code = TotpGenerator.ComputeCodeAt(secret, drifted);

        var verified = TotpGenerator.VerifyCode(secret, code, now, lastUsedStep: null, out _);

        Assert.True(verified);
    }

    [Theory]
    [InlineData(-120)]
    [InlineData(120)]
    public void VerifyCode_RejectsDriftBeyondOneStep(int driftSeconds)
    {
        var secret = TotpGenerator.CreateSecret();
        var now = new DateTime(2026, 3, 14, 9, 30, 0, DateTimeKind.Utc);
        var drifted = now.AddSeconds(driftSeconds);
        var code = TotpGenerator.ComputeCodeAt(secret, drifted);

        var verified = TotpGenerator.VerifyCode(secret, code, now, lastUsedStep: null, out _);

        Assert.False(verified);
    }

    [Fact]
    public void VerifyCode_RejectsAReplayedStep()
    {
        var secret = TotpGenerator.CreateSecret();
        var now = new DateTime(2026, 3, 14, 9, 30, 0, DateTimeKind.Utc);
        var code = TotpGenerator.ComputeCodeAt(secret, now);
        var usedStep = TotpGenerator.GetTimeStep(now);

        var verified = TotpGenerator.VerifyCode(secret, code, now, lastUsedStep: usedStep, out _);

        Assert.False(verified);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData(null)]
    public void VerifyCode_RejectsMalformedCodes(string? candidate)
    {
        var secret = TotpGenerator.CreateSecret();
        var now = new DateTime(2026, 3, 14, 9, 30, 0, DateTimeKind.Utc);

        var verified = TotpGenerator.VerifyCode(secret, candidate, now, lastUsedStep: null, out _);

        Assert.False(verified);
    }

    [Fact]
    public void BuildProvisioningUri_ContainsTheSecretIssuerAndAlgorithm()
    {
        var uri = TotpGenerator.BuildProvisioningUri("Clinic Management", "doctor@clinic.test", RfcSha1Seed);

        Assert.StartsWith("otpauth://totp/Clinic%20Management:doctor%40clinic.test?", uri, StringComparison.Ordinal);
        Assert.Contains($"secret={RfcSha1Seed}", uri, StringComparison.Ordinal);
        Assert.Contains("algorithm=SHA1", uri, StringComparison.Ordinal);
        Assert.Contains("digits=6", uri, StringComparison.Ordinal);
        Assert.Contains("period=30", uri, StringComparison.Ordinal);
    }
}