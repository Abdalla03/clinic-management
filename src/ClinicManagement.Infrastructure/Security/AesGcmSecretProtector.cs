using System.Security.Cryptography;
using System.Text;
using ClinicManagement.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClinicManagement.Infrastructure.Security;

public sealed class AesGcmSecretProtector : ISecretProtector
{
    public const string TotpSecretPurpose = "totp-secret";
    public const string ClinicalFieldPurpose = "clinical-field";

    private const int MasterKeyMinBytes = 32;
    private const int SubkeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const byte FormatVersion = 1;

    private static readonly byte[] HkdfSalt = Encoding.UTF8.GetBytes("clinic-management/hkdf-salt/v1");

    private readonly IReadOnlyDictionary<string, byte[]> _subkeys;
    private readonly EncryptionOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AesGcmSecretProtector> _logger;

    public AesGcmSecretProtector(
        IOptions<EncryptionOptions> options,
        IHostEnvironment environment,
        ILogger<AesGcmSecretProtector> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _environment = environment;
        _logger = logger;
        _subkeys = DeriveSubkeys(ResolveMasterKey());
    }

    public string Protect(string plaintext, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        var (key, aad) = ResolveKeyAndAad(purpose);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagBytes];

        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, aad);

        var payload = new byte[1 + NonceBytes + TagBytes + ciphertext.Length];
        payload[0] = FormatVersion;
        Buffer.BlockCopy(nonce, 0, payload, 1, NonceBytes);
        Buffer.BlockCopy(tag, 0, payload, 1 + NonceBytes, TagBytes);
        Buffer.BlockCopy(ciphertext, 0, payload, 1 + NonceBytes + TagBytes, ciphertext.Length);

        CryptographicOperations.ZeroMemory(plaintextBytes);
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedValue, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);

        var (key, aad) = ResolveKeyAndAad(purpose);

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(protectedValue);
        }
        catch (FormatException exception)
        {
            throw new SecretUnprotectException("The protected value is not valid base64.", exception);
        }

        if (payload.Length < 1 + NonceBytes + TagBytes || payload[0] != FormatVersion)
        {
            throw new SecretUnprotectException("The protected value has an unsupported format.");
        }

        var nonce = payload.AsSpan(1, NonceBytes);
        var tag = payload.AsSpan(1 + NonceBytes, TagBytes);
        var ciphertext = payload.AsSpan(1 + NonceBytes + TagBytes);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        }
        catch (CryptographicException exception)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new SecretUnprotectException("The protected value failed authentication.", exception);
        }

        var result = Encoding.UTF8.GetString(plaintext);
        CryptographicOperations.ZeroMemory(plaintext);
        return result;
    }

    private (byte[] Key, byte[] Aad) ResolveKeyAndAad(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("A protection purpose is required.", nameof(purpose));
        }

        if (!_subkeys.TryGetValue(purpose, out var key))
        {
            throw new ArgumentException(
                $"'{purpose}' is not a registered protection purpose. Registered purposes: {string.Join(", ", _subkeys.Keys)}.",
                nameof(purpose));
        }

        var aad = Encoding.UTF8.GetBytes($"clinic-management/v{FormatVersion}/{purpose}");
        return (key, aad);
    }

    private static IReadOnlyDictionary<string, byte[]> DeriveSubkeys(byte[] masterKey)
    {
        var purposes = new[] { TotpSecretPurpose, ClinicalFieldPurpose };
        var subkeys = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var purpose in purposes)
        {
            subkeys[purpose] = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                masterKey,
                SubkeyBytes,
                HkdfSalt,
                Encoding.UTF8.GetBytes($"clinic-management/purpose/v1/{purpose}"));
        }

        return subkeys;
    }

    private byte[] ResolveMasterKey()
    {
        if (string.IsNullOrWhiteSpace(_options.MasterKeyBase64))
        {
            if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
            {
                var ephemeral = RandomNumberGenerator.GetBytes(MasterKeyMinBytes);
                _logger.LogWarning(
                    "No encryption master key is configured. Generated an ephemeral in-memory key: data protected in this " +
                    "process will become unreadable on restart. Never do this outside Development or Testing.");
                return ephemeral;
            }

            throw new InvalidOperationException(
                $"{EncryptionOptions.SectionName}:{nameof(EncryptionOptions.MasterKeyBase64)} is required outside Development and Testing. " +
                "Generate one with: openssl rand -base64 32");
        }

        byte[] masterKey;
        try
        {
            masterKey = Convert.FromBase64String(_options.MasterKeyBase64);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"{EncryptionOptions.SectionName}:{nameof(EncryptionOptions.MasterKeyBase64)} must be valid base64.", exception);
        }

        if (masterKey.Length < MasterKeyMinBytes)
        {
            CryptographicOperations.ZeroMemory(masterKey);
            throw new InvalidOperationException(
                $"{EncryptionOptions.SectionName}:{nameof(EncryptionOptions.MasterKeyBase64)} must decode to at least {MasterKeyMinBytes} bytes.");
        }

        return masterKey;
    }
}