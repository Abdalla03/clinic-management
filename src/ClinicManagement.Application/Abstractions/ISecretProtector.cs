namespace ClinicManagement.Application.Abstractions;

public interface ISecretProtector
{
    string Protect(string plaintext, string purpose);

    string Unprotect(string protectedValue, string purpose);
}

public sealed class SecretUnprotectException : Exception
{
    public SecretUnprotectException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}