using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace ClinicManagement.Api.Infrastructure;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    internal const string StrictContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    // Swagger UI renders its shell with inline <script> and <style> blocks and pulls its stylesheet,
    // font, image and specification assets from its own path, so default-src 'none' blanks the page.
    // This relaxed policy is used for the documentation path only, and only while developing.
    internal const string SwaggerDevelopmentContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; font-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; connect-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), microphone=(), payment=(), usb=()";
        headers["Content-Security-Policy"] = ContentSecurityPolicy(context);
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            headers.CacheControl = string.Create(
                CultureInfo.InvariantCulture,
                $"no-store, no-cache, must-revalidate, private");
            headers.Pragma = "no-cache";
        }

        await next(context);
    }

    private string ContentSecurityPolicy(HttpContext context)
    {
        var isDocumentationPath = context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);

        return isDocumentationPath && environment.IsDevelopment()
            ? SwaggerDevelopmentContentSecurityPolicy
            : StrictContentSecurityPolicy;
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseClinicSecurityHeaders(this IApplicationBuilder builder) =>
        builder.UseMiddleware<SecurityHeadersMiddleware>();
}
