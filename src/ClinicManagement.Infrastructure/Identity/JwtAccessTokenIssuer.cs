using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClinicManagement.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClinicManagement.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string? SigningKey { get; set; }

    public int AccessTokenMinutes { get; set; } = 15;

    public int MfaPendingTokenMinutes { get; set; } = 10;

    public int RefreshTokenDays { get; set; } = 7;

    public int ClockSkewSeconds { get; set; } = 30;

    public int RefreshTokenBytes { get; set; } = 32;
}

public static class TokenClaimTypes
{
    public const string TokenPurpose = "token_use";
    public const string SecurityStamp = "sec_stamp";
}

public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    public const string FullTokenValue = "full";
    public const string MfaPendingTokenValue = "mfa_pending";

    private readonly JwtOptions _options = options.Value;

    public IssuedToken Issue(TokenSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ValidateOptions(_options);

        var isFull = subject.Purpose == TokenPurpose.Full;
        var lifetime = isFull
            ? TimeSpan.FromMinutes(_options.AccessTokenMinutes)
            : TimeSpan.FromMinutes(_options.MfaPendingTokenMinutes);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.Add(lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, subject.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(TokenClaimTypes.TokenPurpose, isFull ? FullTokenValue : MfaPendingTokenValue),
            new(ClaimTypes.Name, subject.FullName)
        };

        if (!string.IsNullOrWhiteSpace(subject.SecurityStamp))
        {
            claims.Add(new Claim(TokenClaimTypes.SecurityStamp, subject.SecurityStamp));
        }

        claims.AddRange(subject.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey!)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.AddSeconds(-5),
            expires: expires,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public static void ValidateOptions(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            throw new InvalidOperationException($"{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException($"{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)} is required. Generate one with: openssl rand -base64 48");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)} must be at least 32 bytes for HMAC-SHA256.");
        }

        if (options.AccessTokenMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException($"{JwtOptions.SectionName}:{nameof(JwtOptions.AccessTokenMinutes)} must be between 1 and 1440.");
        }

        if (options.MfaPendingTokenMinutes is < 1 or > 30)
        {
            throw new InvalidOperationException($"{JwtOptions.SectionName}:{nameof(JwtOptions.MfaPendingTokenMinutes)} must be between 1 and 30.");
        }

        if (options.RefreshTokenDays is < 1 or > 90)
        {
            throw new InvalidOperationException($"{JwtOptions.SectionName}:{nameof(JwtOptions.RefreshTokenDays)} must be between 1 and 90.");
        }
    }
}