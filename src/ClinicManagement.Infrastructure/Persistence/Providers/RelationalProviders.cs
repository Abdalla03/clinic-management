using Microsoft.EntityFrameworkCore;

namespace ClinicManagement.Infrastructure.Persistence.Providers;

public interface IRelationalProvider
{
    string Name { get; }

    void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString);
}

public sealed class PostgresProvider : IRelationalProvider
{
    public string Name => "PostgreSQL";

    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory");
            npgsql.MigrationsAssembly(typeof(PostgresProvider).Assembly.FullName);
            npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        });
    }
}

public sealed class SqlServerProvider : IRelationalProvider
{
    public string Name => "SqlServer";

    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseSqlServer(connectionString, sqlServer =>
        {
            sqlServer.MigrationsHistoryTable("__EFMigrationsHistory");
            sqlServer.MigrationsAssembly(typeof(SqlServerProvider).Assembly.FullName);
            sqlServer.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        });
    }
}