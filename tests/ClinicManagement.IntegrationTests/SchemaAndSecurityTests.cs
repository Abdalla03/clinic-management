using System.Net;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicManagement.IntegrationTests;

[Collection(ClinicApiCollection.Name)]
public sealed class SchemaAndSecurityTests
{
    private readonly ClinicApiFactory _factory;

    public SchemaAndSecurityTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task Migrations_AreAppliedAndCoreTablesExist()
    {
        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains("2026", string.Join(';', applied));

        var expectedTables = new[]
        {
            "Users", "Roles", "Doctors", "Patients", "Allergies", "MedicalHistoryEntries",
            "PatientVisits", "PatientAttachments", "Appointments", "DoctorSchedules",
            "Prescriptions", "PrescriptionItems", "MedicationReminders", "ConsentRecords",
            "AuditLogEntries", "RefreshTokens"
        };

        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        foreach (var table in expectedTables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass(@name) IS NOT NULL";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "name";
            parameter.Value = $"\"{table}\"";
            command.Parameters.Add(parameter);

            var exists = (bool)(await command.ExecuteScalarAsync())!;
            Assert.True(exists, $"Expected table '{table}' to exist after migration.");
        }
    }

    [RequiresDockerFact]
    public async Task AppointmentSlotIndex_IsUniqueInTheDatabase()
    {
        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT indisunique FROM pg_index WHERE indexrelid = to_regclass('\"UX_Appointments_Doctor_SlotKey\"')";

        var isUnique = (bool)(await command.ExecuteScalarAsync())!;
        Assert.True(isUnique);
    }

    [RequiresDockerFact]
    public async Task Responses_CarryHardeningHeadersAndApiResponsesAreNotCached()
    {
        var client = _factory.CreateClient();

        var root = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, root.StatusCode);

        Assert.Equal("nosniff", Single(root, "X-Content-Type-Options"));
        Assert.Equal("DENY", Single(root, "X-Frame-Options"));
        Assert.Equal("no-referrer", Single(root, "Referrer-Policy"));
        Assert.Contains("default-src 'none'", Single(root, "Content-Security-Policy"), StringComparison.Ordinal);

        var api = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Contains("no-store", Single(api, "Cache-Control"), StringComparison.OrdinalIgnoreCase);
    }

    [RequiresDockerFact]
    public async Task StrictContentSecurityPolicy_IsAppliedToNormalApiPaths()
    {
        var client = _factory.CreateClient();

        foreach (var path in new[] { "/", "/api/auth/me", "/health/ready" })
        {
            var response = await client.GetAsync(path);

            Assert.True(
                Single(response, "Content-Security-Policy")
                    .StartsWith("default-src 'none'", StringComparison.Ordinal),
                $"Expected the strict CSP for '{path}' but got '{Single(response, "Content-Security-Policy")}'.");
        }
    }

    [RequiresDockerFact]
    public async Task SwaggerPath_IsNotGivenTheRelaxedCspOutsideDevelopment()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("default-src 'none'", Single(response, "Content-Security-Policy"), StringComparison.Ordinal);
    }

    [RequiresDockerFact]
    public async Task HealthReadiness_ReportsHealthyWhenTheDatabaseIsReachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string Single(HttpResponseMessage response, string header)
    {
        Assert.True(response.Headers.TryGetValues(header, out var values), $"Header '{header}' is missing.");
        return string.Join(", ", values!);
    }
}