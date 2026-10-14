namespace ClinicManagement.Application.Abstractions;

public enum TokenPurpose
{
    Full = 0,
    MfaPending = 1
}

public sealed record TokenSubject(
    Guid UserId,
    string FullName,
    IReadOnlyCollection<string> Roles,
    string SecurityStamp,
    TokenPurpose Purpose);

public sealed record IssuedToken(string Value, DateTime ExpiresAtUtc);

public interface IAccessTokenIssuer
{
    IssuedToken Issue(TokenSubject subject);
}