using Microsoft.EntityFrameworkCore;

namespace ClinicManagement.Infrastructure.Persistence.Providers;

public static class ProviderNames
{
    public const string PostgreSql = "PostgreSQL";
    public const string SqlServer = "SqlServer";
}

public static class ProviderConfiguration
{
    public static void ConfigureProvider(DbContextOptionsBuilder optionsBuilder, string providerName, string connectionString)
    {
        switch (providerName.ToLowerInvariant())
        {
            case "postgresql":
            case "postgres":
            case "npgsql":
                new PostgresProvider().Configure(optionsBuilder, connectionString);
                break;
            case "sqlserver":
            case "mssql":
                new SqlServerProvider().Configure(optionsBuilder, connectionString);
                break;
            default:
                throw new InvalidOperationException(
                    $"Database provider '{providerName}' is not supported. Use '{ProviderNames.PostgreSql}' or '{ProviderNames.SqlServer}'.");
        }
    }
}