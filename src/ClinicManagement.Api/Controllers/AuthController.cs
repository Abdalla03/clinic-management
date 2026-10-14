using ClinicManagement.Api.Authorization;
using ClinicManagement.Api.Infrastructure;
using ClinicManagement.Application.Auth;
using ClinicManagement.Application.Auth.Contracts;
using ClinicManagement.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClinicManagement.Api.Controllers;

[Route("api/auth")]
[Authorize(Policy = Policies.AuthenticatedUser)]
public sealed class AuthController(IAuthService authService) : ClinicControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    public async Task<IResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);

        if (result.Failed)
        {
            return result.ToHttpResult();
        }

        return Results.Ok(result.Value);
    }

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Mfa)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    public async Task<IResult> VerifyMfaAsync([FromBody] MfaVerifyRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.VerifyMfaAsync(request, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    public async Task<IResult> RefreshAsync([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> LogoutAsync([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(request, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    public async Task<IResult> MeAsync(CancellationToken cancellationToken)
    {
        var result = await authService.GetCurrentUserAsync(cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("mfa/enroll")]
    [ProducesResponseType(typeof(MfaEnrollment), StatusCodes.Status200OK)]
    public async Task<IResult> BeginMfaEnrollmentAsync(CancellationToken cancellationToken)
    {
        var result = await authService.BeginMfaEnrollmentAsync(cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("mfa/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> ConfirmMfaAsync([FromBody] MfaCodeRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.ConfirmMfaEnrollmentAsync(request, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("mfa/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> DisableMfaAsync([FromBody] DisableMfaRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.DisableMfaAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}

public static class RateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Mfa = "auth-mfa";
}