using ClinicManagement.Application.Common;

namespace ClinicManagement.Application.Auth;

public static class AuthErrorCodes
{
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string AccountLocked = "auth.account_locked";
    public const string AccountDisabled = "auth.account_disabled";
    public const string MfaInvalidCode = "auth.mfa_invalid_code";
    public const string MfaAlreadyEnabled = "auth.mfa_already_enabled";
    public const string MfaNotEnabled = "auth.mfa_not_enabled";
    public const string TokenInvalid = "auth.token_invalid";
    public const string RefreshTokenInvalid = "auth.refresh_token_invalid";
}