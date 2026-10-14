namespace ClinicManagement.Application.Auth.Contracts;

public sealed record LoginRequest(string EmailOrUserName, string Password);

public sealed record MfaVerifyRequest(string MfaPendingToken, string Code);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record MfaCodeRequest(string Code);

public sealed record DisableMfaRequest(string Password);

public sealed record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    string TokenType,
    IReadOnlyCollection<string> Roles,
    string FullName);

public sealed record LoginResult(
    bool MfaRequired,
    string? MfaPendingToken,
    DateTime? MfaPendingTokenExpiresAtUtc,
    TokenResponse? Tokens);

public sealed record MfaEnrollment(string Base32Secret, string ProvisioningUri);

public sealed record CurrentUserResponse(
    Guid UserId,
    string FullName,
    string Email,
    IReadOnlyCollection<string> Roles,
    bool MfaEnabled);