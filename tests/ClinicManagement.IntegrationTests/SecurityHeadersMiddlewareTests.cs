using ClinicManagement.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace ClinicManagement.IntegrationTests;

public sealed class SecurityHeadersMiddlewareTests
{
    [Theory]
    [InlineData("Development", "/swagger/index.html", false)]
    [InlineData("Development", "/swagger/v1/swagger.json", false)]
    [InlineData("Development", "/api/auth/me", true)]
    [InlineData("Development", "/health/ready", true)]
    [InlineData("Development", "/", true)]
    [InlineData("Testing", "/swagger/index.html", true)]
    [InlineData("Production", "/swagger/index.html", true)]
    [InlineData("Production", "/api/auth/me", true)]
    public async Task ContentSecurityPolicy_IsStrictExceptForSwaggerWhileDeveloping(
        string environmentName,
        string path,
        bool expectStrictPolicy)
    {
        var policy = await InvokeAsync(environmentName, path);

        if (expectStrictPolicy)
        {
            Assert.Equal(
                "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'",
                policy);
        }
        else
        {
            Assert.DoesNotContain("default-src 'none'", policy, StringComparison.Ordinal);
            Assert.Contains("default-src 'self'", policy, StringComparison.Ordinal);
            Assert.Contains("script-src 'self' 'unsafe-inline'", policy, StringComparison.Ordinal);
            Assert.Contains("style-src 'self' 'unsafe-inline'", policy, StringComparison.Ordinal);
            Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task HardeningHeaders_AreAlwaysSetEvenWhenTheCspIsRelaxed()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/swagger/index.html";
        context.Response.Body = new MemoryStream();

        var middleware = new SecurityHeadersMiddleware(
            _ => Task.CompletedTask,
            new StubWebHostEnvironment("Development"));

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Opener-Policy"].ToString());
    }

    private static async Task<string> InvokeAsync(string environmentName, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var middleware = new SecurityHeadersMiddleware(
            _ => Task.CompletedTask,
            new StubWebHostEnvironment(environmentName));

        await middleware.InvokeAsync(context);

        return context.Response.Headers["Content-Security-Policy"].ToString();
    }

    private sealed class StubWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ClinicManagement.Api";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = environmentName;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    }
}
