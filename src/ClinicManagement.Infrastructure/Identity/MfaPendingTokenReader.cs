using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClinicManagement.Infrastructure.Identity;

public sealed class MfaPendingTokenReader(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public Guid? ReadUserId(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey ?? string.Empty)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwt ||
                !jwt.Claims.Any(claim => claim.Type == TokenClaimTypes.TokenPurpose && claim.Value == JwtAccessTokenIssuer.MfaPendingTokenValue))
            {
                return null;
            }

            var subject = jwt.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Sub)?.Value
                          ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(subject, out var userId) ? userId : null;
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException or FormatException)
        {
            return null;
        }
    }
}