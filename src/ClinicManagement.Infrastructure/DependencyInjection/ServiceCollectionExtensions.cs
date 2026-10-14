using ClinicManagement.Application.Abstractions;
using ClinicManagement.Application.Appointments;
using ClinicManagement.Application.Auth;
using ClinicManagement.Application.Auth.Validation;
using ClinicManagement.Application.Billing;
using ClinicManagement.Application.Patients;
using ClinicManagement.Application.Prescriptions;
using ClinicManagement.Application.Reports;
using ClinicManagement.Application.Visits;
using ClinicManagement.Infrastructure.Appointments;
using ClinicManagement.Infrastructure.Auditing;
using ClinicManagement.Infrastructure.Billing;
using ClinicManagement.Infrastructure.Identity;
using ClinicManagement.Infrastructure.Patients;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.Infrastructure.Persistence.Providers;
using ClinicManagement.Infrastructure.Prescriptions;
using ClinicManagement.Infrastructure.Reports;
using ClinicManagement.Infrastructure.Seeding;
using ClinicManagement.Infrastructure.Security;
using ClinicManagement.Infrastructure.Visits;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClinicManagement.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public const string ConnectionStringName = "ClinicDb";

    public static IServiceCollection AddClinicPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var providerName = configuration["Database:Provider"] ?? PostgresProviderName;
        var connectionString = ResolveConnectionString(configuration);

        var provider = ResolveProvider(providerName);
        services.AddSingleton<IRelationalProvider>(provider);

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddDbContext<ClinicDbContext>(options => provider.Configure(options, connectionString));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<DemoAccountSeeder>();
        return services;
    }

    public static IServiceCollection AddClinicSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<MfaPendingTokenReader>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        var shouldValidateNow = !environment.IsDevelopment() && !environment.IsEnvironment("Testing");
        if (shouldValidateNow)
        {
            JwtAccessTokenIssuer.ValidateOptions(configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions());
            _ = new AesGcmSecretProtector(
                Microsoft.Extensions.Options.Options.Create(configuration.GetSection(EncryptionOptions.SectionName).Get<EncryptionOptions>() ?? new EncryptionOptions()),
                environment,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AesGcmSecretProtector>.Instance);
        }

        return services;
    }

    public static IServiceCollection AddClinicIdentity(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<ClinicDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<PasswordHasherOptions>(options =>
        {
            options.IterationCount = 210_000;
            options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
        });

        services.AddScoped<IAuthService, AuthService>();
        return services;
    }

    public static IServiceCollection AddClinicValidators(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(ServiceLifetime.Scoped);
        return services;
    }

    public static IServiceCollection AddClinicApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IVisitService, VisitService>();
        services.AddScoped<IPrescriptionService, PrescriptionService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IReportService, ReportService>();
        return services;
    }

    private static IRelationalProvider ResolveProvider(string providerName) => providerName.ToLowerInvariant() switch
    {
        "postgresql" or "postgres" or "npgsql" => new PostgresProvider(),
        "sqlserver" or "mssql" => new SqlServerProvider(),
        _ => throw new InvalidOperationException(
            $"Database:Provider '{providerName}' is not supported. Use 'PostgreSQL' or 'SqlServer'.")
    };

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString(ConnectionStringName);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var host = configuration["Database:Host"] ?? "localhost";
        var port = configuration["Database:Port"] ?? "5432";
        var name = configuration["Database:Name"] ?? "clinic";
        var user = configuration["Database:User"] ?? configuration["Database:Username"] ?? "clinic";
        var password = configuration["Database:Password"]
            ?? throw new InvalidOperationException(
                "No connection string configured. Set ConnectionStrings:ClinicDb or Database:Password.");

        return $"Host={host};Port={port};Database={name};Username={user};Password={password};Include Error Detail=false";
    }

    private const string PostgresProviderName = "PostgreSQL";
}