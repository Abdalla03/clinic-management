using ClinicManagement.Application.Abstractions;
using ClinicManagement.Infrastructure.Persistence.Providers;
using ClinicManagement.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClinicManagement.Infrastructure.Persistence;

public sealed class ClinicDbContextFactory : IDesignTimeDbContextFactory<ClinicDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=clinic;Username=clinic;Password=clinic;Include Error Detail=false";

    public ClinicDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__ClinicDb") ??
            args.FirstOrDefault(argument => argument.StartsWith("--connection=", StringComparison.Ordinal))?["--connection=".Length..] ??
            DefaultConnectionString;

        var provider = args.Contains("--sqlserver", StringComparer.OrdinalIgnoreCase)
            ? ProviderNames.SqlServer
            : ProviderNames.PostgreSql;

        var optionsBuilder = new DbContextOptionsBuilder<ClinicDbContext>();
        ProviderConfiguration.ConfigureProvider(optionsBuilder, provider, connectionString);

        return new ClinicDbContext(optionsBuilder.Options, CreateFieldProtector());
    }

    // Design time never writes data, it only shapes migrations, so it reuses the same protector the
    // runtime uses: Development and Testing fall back to an ephemeral key, everywhere else a real
    // Encryption:MasterKeyBase64 must be configured, exactly as the running application requires.
    private static ISecretProtector CreateFieldProtector()
    {
        var masterKey = Environment.GetEnvironmentVariable("Encryption__MasterKeyBase64");

        return new AesGcmSecretProtector(
            Options.Create(new EncryptionOptions { MasterKeyBase64 = masterKey }),
            new DesignTimeEnvironment(),
            NullLogger<AesGcmSecretProtector>.Instance);
    }

    private sealed class DesignTimeEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "ClinicManagement.Infrastructure";

        public string EnvironmentName { get; set; } =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environments.Development;

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
