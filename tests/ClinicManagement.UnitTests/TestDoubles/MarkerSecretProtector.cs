using ClinicManagement.Application.Abstractions;

namespace ClinicManagement.UnitTests.TestDoubles;

/// <summary>
/// A deterministic stand-in for <see cref="AesGcmSecretProtector"/>. It is only ever used to prove
/// that the model wires a protector into the value converters; real cryptography is covered by
/// <c>AesGcmSecretProtectorTests</c>.
/// </summary>
public sealed class MarkerSecretProtector : ISecretProtector
{
    public string Protect(string plaintext, string purpose) => $"cipher|{purpose}|{plaintext}";

    public string Unprotect(string ciphertext, string purpose)
    {
        var prefix = $"cipher|{purpose}|";

        return ciphertext.StartsWith(prefix, StringComparison.Ordinal)
            ? ciphertext[prefix.Length..]
            : throw new FormatException("Ciphertext was written with a different purpose.");
    }
}
