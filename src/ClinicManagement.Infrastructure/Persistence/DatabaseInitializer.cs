using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClinicManagement.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending migrations, guarantees the Identity roles exist and, when the host is running
    /// in Development or Testing, creates the demo logins the README documents.
    /// </summary>
    public static async Task InitializeAsync(
        IServiceProvider services,
        bool seedDemoAccounts,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var dbContext = provider.GetRequiredService<ClinicDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        if (seedDemoAccounts)
        {
            var seeded = await provider.GetRequiredService<DemoAccountSeeder>().SeedAsync(cancellationToken);
            logger.LogInformation("Database migrated, roles ensured, {Seeded} demo accounts seeded.", seeded);
        }
        else
        {
            logger.LogInformation("Database migrated and roles ensured.");
        }
    }
}
