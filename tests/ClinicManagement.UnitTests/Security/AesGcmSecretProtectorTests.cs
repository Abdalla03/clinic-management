using System.Security.Cryptography;
using ClinicManagement.Application.Abstractions;
using ClinicManagement.Infrastructure.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ClinicManagement.UnitTests.Security;

public sealed class AesGcmSecretProtectorTests
{
    private static readonly byte[] MasterKey = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void ProtectThenUnprotect_RestoresThePlaintext()
    {
        var protector = CreateProtector();

        var protectedValue = protector.Protect("JBSWY3DPEHPK3PXP", AesGcmSecretProtector.TotpSecretPurpose);
        var plaintext = protector.Unprotect(protectedValue, AesGcmSecretProtector.TotpSecretPurpose);

        Assert.Equal("JBSWY3DPEHPK3PXP", plaintext);
    }

    [Fact]
    public void Protect_UsesAFreshNonceForEveryCall()
    {
        var protector = CreateProtector();

        var first = protector.Protect("same-plaintext", AesGcmSecretProtector.TotpSecretPurpose);
        var second = protector.Protect("same-plaintext", AesGcmSecretProtector.TotpSecretPurpose);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Unprotect_FailsWhenTheCiphertextIsTamperedWith()
    {
        var protector = CreateProtector();
        var protectedValue = protector.Protect("secret", AesGcmSecretProtector.TotpSecretPurpose);

        var payload = Convert.FromBase64String(protectedValue);
        payload[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(payload);

        Assert.Throws<SecretUnprotectException>(() => protector.Unprotect(tampered, AesGcmSecretProtector.TotpSecretPurpose));
    }

    [Fact]
    public void Unprotect_FailsWhenThePurposeDoesNotMatch()
    {
        var protector = CreateProtector();
        var protectedValue = protector.Protect("secret", AesGcmSecretProtector.TotpSecretPurpose);

        Assert.Throws<SecretUnprotectException>(() =>
            protector.Unprotect(protectedValue, AesGcmSecretProtector.ClinicalFieldPurpose));
    }

    [Fact]
    public void Unprotect_RejectsAnUnregisteredPurpose()
    {
        var protector = CreateProtector();
        var protectedValue = protector.Protect("secret", AesGcmSecretProtector.TotpSecretPurpose);

        var exception = Assert.Throws<ArgumentException>(() =>
            protector.Unprotect(protectedValue, "some-other-purpose"));

        Assert.Contains("not a registered protection purpose", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PurposeDerivedKeysDiffer()
    {
        var protector = CreateProtector();

        var totpCiphertext = protector.Protect("value", AesGcmSecretProtector.TotpSecretPurpose);
        var clinicalCiphertext = protector.Protect("value", AesGcmSecretProtector.ClinicalFieldPurpose);

        Assert.NotEqual(totpCiphertext, clinicalCiphertext);
    }

    [Fact]
    public void Constructor_ThrowsOutsideDevelopmentWhenNoMasterKeyIsConfigured()
    {
        var options = Options.Create(new EncryptionOptions { MasterKeyBase64 = null });
        var environment = new StubHostEnvironment { EnvironmentName = Environments.Production };

        Assert.Throws<InvalidOperationException>(() =>
            new AesGcmSecretProtector(options, environment, NullLogger<AesGcmSecretProtector>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsWhenTheMasterKeyIsTooShort()
    {
        var options = Options.Create(new EncryptionOptions
        {
            MasterKeyBase64 = Convert.ToBase64String(new byte[16])
        });

        var environment = new StubHostEnvironment { EnvironmentName = Environments.Development };

        Assert.Throws<InvalidOperationException>(() =>
            new AesGcmSecretProtector(options, environment, NullLogger<AesGcmSecretProtector>.Instance));
    }

    [Fact]
    public void Constructor_GeneratesAnEphemeralKeyInDevelopment()
    {
        var options = Options.Create(new EncryptionOptions { MasterKeyBase64 = null });
        var environment = new StubHostEnvironment { EnvironmentName = Environments.Development };

        var protector = new AesGcmSecretProtector(options, environment, NullLogger<AesGcmSecretProtector>.Instance);
        var protectedValue = protector.Protect("dev", AesGcmSecretProtector.TotpSecretPurpose);

        Assert.Equal("dev", protector.Unprotect(protectedValue, AesGcmSecretProtector.TotpSecretPurpose));
    }

    private static AesGcmSecretProtector CreateProtector()
    {
        var options = Options.Create(new EncryptionOptions
        {
            MasterKeyBase64 = Convert.ToBase64String(MasterKey)
        });

        var environment = new StubHostEnvironment { EnvironmentName = Environments.Production };

        return new AesGcmSecretProtector(options, environment, NullLogger<AesGcmSecretProtector>.Instance);
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "ClinicManagement.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}