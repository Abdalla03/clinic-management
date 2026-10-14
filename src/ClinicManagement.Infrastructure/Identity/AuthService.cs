using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClinicManagement.Application.Abstractions;
using ClinicManagement.Application.Auth;
using ClinicManagement.Application.Auth.Contracts;
using ClinicManagement.Application.Auth.Validation;
using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Extensions;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.Infrastructure.Security;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClinicManagement.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    ClinicDbContext dbContext,
    IAccessTokenIssuer tokenIssuer,
    ITotpService totpService,
    ISecretProtector secretProtector,
    MfaPendingTokenReader mfaPendingTokenReader,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private const string AuthenticatorIssuer = "Clinic Management";
    private const string MfaMandatoryForRole = "auth.mfa_mandatory_for_role";

    private static readonly string[] RolesRequiringMfa = [RoleNames.Doctor, RoleNames.Secretary];

    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly string _dummyPasswordHash = passwordHasher.HashPassword(
        new ApplicationUser { Id = Guid.NewGuid() },
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public async Task<Result<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await new LoginRequestValidator().ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result<LoginResult>.Failure(validation.Error!);
        }

        var user = await FindUserAsync(request.EmailOrUserName);

        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(new ApplicationUser(), _dummyPasswordHash, request.Password);
            logger.LogWarning("Login attempt for an unknown account was rejected.");
            return Result<LoginResult>.Failure(AuthErrorCodes.InvalidCredentials, "Invalid email, username or password.");
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Login attempt for a disabled account was rejected.");
            return Result<LoginResult>.Failure(AuthErrorCodes.AccountDisabled, "This account is disabled.");
        }

        var signIn = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (signIn.IsLockedOut)
        {
            logger.LogWarning("Login attempt for a locked-out account was rejected.");
            return Result<LoginResult>.Failure(AuthErrorCodes.AccountLocked, "This account is temporarily locked. Try again later.");
        }

        if (!signIn.Succeeded)
        {
            logger.LogWarning("Login attempt failed the password check.");
            return Result<LoginResult>.Failure(AuthErrorCodes.InvalidCredentials, "Invalid email, username or password.");
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();

        if (user.MfaSatisfied)
        {
            var pending = tokenIssuer.Issue(new TokenSubject(
                user.Id,
                user.FullName,
                roles,
                user.SecurityStamp ?? string.Empty,
                TokenPurpose.MfaPending));

            return Result<LoginResult>.Success(new LoginResult(
                MfaRequired: true,
                pending.Value,
                pending.ExpiresAtUtc,
                Tokens: null));
        }

        var session = await IssueSessionAsync(user, roles, cancellationToken);
        if (session.Failed)
        {
            return Result<LoginResult>.Failure(session.Error!);
        }

        return Result<LoginResult>.Success(new LoginResult(
            MfaRequired: false,
            MfaPendingToken: null,
            MfaPendingTokenExpiresAtUtc: null,
            Tokens: session.Value));
    }

    public async Task<Result<TokenResponse>> VerifyMfaAsync(MfaVerifyRequest request, CancellationToken cancellationToken)
    {
        var validation = await new MfaVerifyRequestValidator().ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result<TokenResponse>.Failure(validation.Error!);
        }

        var userId = mfaPendingTokenReader.ReadUserId(request.MfaPendingToken);
        if (userId is null)
        {
            return Result<TokenResponse>.Failure(AuthErrorCodes.TokenInvalid, "The MFA session is invalid or has expired.");
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null || !user.IsActive)
        {
            return Result<TokenResponse>.Failure(AuthErrorCodes.TokenInvalid, "The MFA session is invalid or has expired.");
        }

        if (!user.MfaSatisfied)
        {
            return Result<TokenResponse>.Failure(AuthErrorCodes.MfaNotEnabled, "Multi-factor authentication is not enabled for this account.");
        }

        var secret = DecryptTotpSecret(user);
        var verify = totpService.VerifyCode(
            secret,
            request.Code,
            timeProvider.GetUtcNow().UtcDateTime,
            user.LastUsedTotpStep,
            out var matchedStep);

        if (verify.Failed)
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("MFA verification failed.");
            return Result<TokenResponse>.Failure(verify.Error!);
        }

        user.LastUsedTotpStep = matchedStep;
        user.LastLoginAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            logger.LogError("Failed to persist MFA verification state: {Errors}", string.Join("; ", update.Errors.Select(error => error.Code)));
            return Result<TokenResponse>.Failure(AuthErrorCodes.TokenInvalid, "The MFA session could not be completed.");
        }

        logger.LogInformation("MFA verification succeeded.");

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        return await IssueSessionAsync(user, roles, cancellationToken);
    }

    public async Task<Result<TokenResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var validation = await new RefreshRequestValidator().ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result<TokenResponse>.Failure(validation.Error!);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hash = HashToken(request.RefreshToken);

        var stored = await dbContext.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (stored is null)
        {
            return Result<TokenResponse>.Failure(AuthErrorCodes.RefreshTokenInvalid, "The refresh token is invalid.");
        }

        if (stored.RevokedAtUtc is not null)
        {
            await RevokeAllForUserAsync(stored.ApplicationUserId, now);
            logger.LogWarning("Reuse of a revoked refresh token was detected; all sessions for that account were revoked.");
            return Result<TokenResponse>.Failure(AuthErrorCodes.RefreshTokenInvalid, "The refresh token is invalid.");
        }

        if (stored.ExpiresAtUtc <= now)
        {
            return Result<TokenResponse>.Failure(AuthErrorCodes.RefreshTokenInvalid, "The refresh token has expired.");
        }

        var user = await userManager.FindByIdAsync(stored.ApplicationUserId.ToString());
        if (user is null || !user.IsActive || user.AccessRevokedAtUtc is not null)
        {
            return Result<TokenResponse>.Failure(AuthErrorCodes.RefreshTokenInvalid, "The refresh token is invalid.");
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();

        var securityStamp = user.SecurityStamp ?? string.Empty;
        if (!string.Equals(stored.SecurityStamp, securityStamp, StringComparison.Ordinal))
        {
            logger.LogWarning("Refresh was refused because the account security stamp changed after this token was issued.");
            await RevokeAllForUserAsync(user.Id, now);
            return Result<TokenResponse>.Failure(AuthErrorCodes.RefreshTokenInvalid, "The refresh token is invalid.");
        }

        if (RequiresMfa(roles) && user.TotpEnabledAtUtc is { } enrolledAtUtc && stored.CreatedAtUtc < enrolledAtUtc)
        {
            logger.LogWarning("Refresh was refused because MFA was enabled after this session was issued.");
            await RevokeAllForUserAsync(user.Id, now);
            return Result<TokenResponse>.Failure(AuthErrorCodes.MfaNotEnabled, "Sign in again to complete multi-factor authentication.");
        }

        var (replacement, rawRefreshToken) = CreateRefreshToken(user.Id, now, securityStamp);
        stored.Revoke(now, replacement.Id);
        dbContext.RefreshTokens.Add(replacement);

        var accessToken = tokenIssuer.Issue(new TokenSubject(
            user.Id,
            user.FullName,
            roles,
            securityStamp,
            TokenPurpose.Full));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TokenResponse>.Success(new TokenResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            rawRefreshToken,
            replacement.ExpiresAtUtc,
            "Bearer",
            roles,
            user.FullName));
    }

    public async Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hash = HashToken(request.RefreshToken);

        var stored = await dbContext.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (stored is not null && stored.RevokedAtUtc is null)
        {
            stored.Revoke(now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<MfaEnrollment>> BeginMfaEnrollmentAsync(CancellationToken cancellationToken)
    {
        var user = await RequireCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Result<MfaEnrollment>.Failure(AuthErrorCodes.TokenInvalid, "The caller is not authenticated.");
        }

        if (user.MfaSatisfied)
        {
            return Result<MfaEnrollment>.Failure(AuthErrorCodes.MfaAlreadyEnabled, "Multi-factor authentication is already enabled.");
        }

        var secret = totpService.CreateSecret();
        user.TotpSecretCiphertext = secretProtector.Protect(secret, AesGcmSecretProtector.TotpSecretPurpose);
        user.LastUsedTotpStep = null;

        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            logger.LogError("Failed to persist the MFA enrollment secret: {Errors}", string.Join("; ", update.Errors.Select(error => error.Code)));
            return Result<MfaEnrollment>.Failure(AuthErrorCodes.TokenInvalid, "MFA enrollment could not be started.");
        }

        var accountLabel = string.IsNullOrWhiteSpace(user.Email) ? user.Id.ToString() : user.Email;
        var provisioningUri = totpService.GetProvisioningUri(AuthenticatorIssuer, accountLabel, secret);

        return Result<MfaEnrollment>.Success(new MfaEnrollment(secret, provisioningUri));
    }

    public async Task<Result> ConfirmMfaEnrollmentAsync(MfaCodeRequest request, CancellationToken cancellationToken)
    {
        var validation = await new MfaCodeRequestValidator().ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result.Failure(validation.Error!);
        }

        var user = await RequireCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Result.Failure(AuthErrorCodes.TokenInvalid, "The caller is not authenticated.");
        }

        if (user.MfaSatisfied)
        {
            return Result.Failure(AuthErrorCodes.MfaAlreadyEnabled, "Multi-factor authentication is already enabled.");
        }

        if (string.IsNullOrWhiteSpace(user.TotpSecretCiphertext))
        {
            return Result.Failure(AuthErrorCodes.MfaNotEnabled, "Start MFA enrollment before confirming it.");
        }

        var secret = DecryptTotpSecret(user);
        var verify = totpService.VerifyCode(
            secret,
            request.Code,
            timeProvider.GetUtcNow().UtcDateTime,
            lastUsedStep: null,
            out var matchedStep);

        if (verify.Failed)
        {
            return Result.Failure(verify.Error!);
        }

        user.TotpEnabledAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        user.LastUsedTotpStep = matchedStep;

        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            logger.LogError("Failed to enable MFA: {Errors}", string.Join("; ", update.Errors.Select(error => error.Code)));
            return Result.Failure(AuthErrorCodes.TokenInvalid, "MFA could not be enabled.");
        }

        logger.LogInformation("MFA was enabled for an account.");
        return Result.Success();
    }

    public async Task<Result> DisableMfaAsync(DisableMfaRequest request, CancellationToken cancellationToken)
    {
        var validation = await new DisableMfaRequestValidator().ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result.Failure(validation.Error!);
        }

        var user = await RequireCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Result.Failure(AuthErrorCodes.TokenInvalid, "The caller is not authenticated.");
        }

        if (!user.MfaSatisfied)
        {
            return Result.Success();
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        if (roles.Any(role => RolesRequiringMfa.Contains(role)))
        {
            logger.LogWarning("An attempt to disable MFA was blocked for a role that requires it.");
            return Result.Failure(MfaMandatoryForRole, "Multi-factor authentication cannot be disabled for this role.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Result.Failure(AuthErrorCodes.InvalidCredentials, "Invalid email, username or password.");
        }

        user.TotpEnabledAtUtc = null;
        user.TotpSecretCiphertext = null;
        user.LastUsedTotpStep = null;

        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            logger.LogError("Failed to disable MFA: {Errors}", string.Join("; ", update.Errors.Select(error => error.Code)));
            return Result.Failure(AuthErrorCodes.TokenInvalid, "MFA could not be disabled.");
        }

        await RevokeAllForUserAsync(user.Id, timeProvider.GetUtcNow().UtcDateTime);
        logger.LogInformation("MFA was disabled for an account.");
        return Result.Success();
    }

    public async Task<Result<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = await RequireCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Result<CurrentUserResponse>.Failure(AuthErrorCodes.TokenInvalid, "The caller is not authenticated.");
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        return Result<CurrentUserResponse>.Success(new CurrentUserResponse(
            user.Id,
            user.FullName,
            user.Email ?? string.Empty,
            roles,
            user.MfaSatisfied));
    }

    private async Task<Result<TokenResponse>> IssueSessionAsync(
        ApplicationUser user,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var securityStamp = user.SecurityStamp ?? string.Empty;
        var accessToken = tokenIssuer.Issue(new TokenSubject(
            user.Id,
            user.FullName,
            roles,
            securityStamp,
            TokenPurpose.Full));

        var (refreshEntity, refreshRawToken) = CreateRefreshToken(user.Id, now, securityStamp);
        dbContext.RefreshTokens.Add(refreshEntity);

        user.LastLoginAtUtc = now;
        await userManager.UpdateAsync(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("A session was issued.");

        return Result<TokenResponse>.Success(new TokenResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshRawToken,
            refreshEntity.ExpiresAtUtc,
            "Bearer",
            roles,
            user.FullName));
    }

    private (RefreshToken Entity, string RawToken) CreateRefreshToken(Guid userId, DateTime nowUtc, string securityStamp)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(_jwtOptions.RefreshTokenBytes));
        var entity = RefreshToken.Create(
            userId,
            RefreshTokenHasher.Hash(rawToken),
            nowUtc,
            nowUtc.AddDays(_jwtOptions.RefreshTokenDays),
            securityStamp);

        return (entity, rawToken);
    }

    private async Task<ApplicationUser?> RequireCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        return user is { IsActive: true } ? user : null;
    }

    private async Task<ApplicationUser?> FindUserAsync(string identifier)
    {
        var user = await userManager.FindByNameAsync(identifier);
        if (user is not null)
        {
            return user;
        }

        return await userManager.FindByEmailAsync(identifier);
    }

    private string DecryptTotpSecret(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.TotpSecretCiphertext))
        {
            throw new InvalidOperationException("The account has no MFA secret.");
        }

        return secretProtector.Unprotect(user.TotpSecretCiphertext, AesGcmSecretProtector.TotpSecretPurpose);
    }

    private async Task RevokeAllForUserAsync(Guid userId, DateTime nowUtc)
    {
        var active = await dbContext.RefreshTokens
            .Where(token => token.ApplicationUserId == userId && token.RevokedAtUtc == null)
            .ToListAsync();

        foreach (var token in active)
        {
            token.Revoke(nowUtc);
        }

        await dbContext.SaveChangesAsync();
    }

    private static string HashToken(string token) => RefreshTokenHasher.Hash(token);

    private static bool RequiresMfa(IReadOnlyCollection<string> roles) =>
        roles.Any(role => RolesRequiringMfa.Contains(role));
}