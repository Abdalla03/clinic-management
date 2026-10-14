using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace ClinicManagement.Api.Authorization;

public static class Policies
{
    /// <summary>Any signed-in user with a fully verified session, regardless of role.</summary>
    public const string AuthenticatedUser = "AuthenticatedUser";

    public const string DoctorOnly = "DoctorOnly";
    public const string SecretaryOnly = "SecretaryOnly";
    public const string PatientOnly = "PatientOnly";
    public const string ClinicalStaff = "ClinicalStaff";
}

public static class PolicyFactory
{
    public static void ApplyMfaPolicy(AuthorizationPolicyBuilder builder, params string[] roles)
    {
        // Identity registers a cookie default scheme; without pinning the JWT scheme here a policy
        // would challenge with the cookie handler and never see the bearer principal.
        if (!builder.AuthenticationSchemes.Contains(JwtBearerDefaults.AuthenticationScheme))
        {
            builder.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
        }

        builder
            .RequireAuthenticatedUser()
            .AddRequirements(new MfaSatisfiedRequirement());

        if (roles.Length > 0)
        {
            builder.RequireRole(roles);
        }
    }

    public static AuthorizationPolicy MfaSatisfied(params string[] roles)
    {
        var builder = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme);
        ApplyMfaPolicy(builder, roles);
        return builder.Build();
    }
}

public sealed class MfaSatisfiedRequirement : IAuthorizationRequirement
{
}

public sealed class MfaSatisfiedHandler : AuthorizationHandler<MfaSatisfiedRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MfaSatisfiedRequirement requirement)
    {
        var tokenPurpose = context.User.FindFirstValue(TokenClaimTypes.TokenPurpose);
        if (string.Equals(tokenPurpose, JwtAccessTokenIssuer.FullTokenValue, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class AuthorizationDefaults
{
    public static string[] StaffRoles => [RoleNames.Doctor, RoleNames.Secretary];
}