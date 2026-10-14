using System.Security.Cryptography;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace ClinicManagement.IntegrationTests.Infrastructure;

public sealed class ClinicApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DatabaseName = "clinic_tests";
    private const string DatabaseUser = "clinic_tests";
    private const string DatabasePassword = "clinic_tests_password";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase(DatabaseName)
        .WithUsername(DatabaseUser)
        .WithPassword(DatabasePassword)
        .WithCleanUp(true)
        .Build();

    private readonly Dictionary<string, string> _environmentVariables = new(StringComparer.Ordinal)
    {
        ["Database__Provider"] = "PostgreSQL",
        ["Jwt__Issuer"] = "clinic-tests",
        ["Jwt__Audience"] = "clinic-tests-client",
        ["Jwt__SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
        ["Clinic__Encryption__MasterKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["Cors__AllowedOrigins__0"] = "https://clinic.test",
        ["RateLimiting__AuthPerMinute"] = "10000",
        ["RateLimiting__MfaPerMinute"] = "10000",
        ["RateLimiting__ApiPerMinute"] = "100000"
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Program reads configuration while registering services, so it must already be present when the
        // host is first built. Environment variables are the only channel guaranteed to be visible to
        // WebApplication.CreateBuilder for minimal-hosting apps.
        _environmentVariables["ConnectionStrings__ClinicDb"] = _database.GetConnectionString();
        foreach (var (key, value) in _environmentVariables)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        await DatabaseInitializer.InitializeAsync(Services, seedDemoAccounts: true);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();

        foreach (var key in _environmentVariables.Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    public IServiceScope CreateScope() => Services.CreateScope();
}

[CollectionDefinition(Name)]
public sealed class ClinicApiCollection : ICollectionFixture<ClinicApiFactory>
{
    public const string Name = "clinic-api";
}