using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ClinicManagement.Api.Infrastructure;

public sealed class ClinicRateLimiterOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPerMinute { get; set; } = 5;

    public int MfaPerMinute { get; set; } = 10;

    public int ApiPerMinute { get; set; } = 300;
}

public static class RateLimiterRegistration
{
    public static IServiceCollection AddClinicRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ClinicRateLimiterOptions.SectionName).Get<ClinicRateLimiterOptions>()
                      ?? new ClinicRateLimiterOptions();

        services.AddSingleton(options);
        services.AddRateLimiter(rateLimiter =>
        {
            rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            rateLimiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.ApiPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            rateLimiter.AddPolicy(Controllers.RateLimitPolicies.Login, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.AuthPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            rateLimiter.AddPolicy(Controllers.RateLimitPolicies.Mfa, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.MfaPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    private static string PartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}