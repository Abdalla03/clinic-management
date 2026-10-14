using ClinicManagement.Application.Abstractions;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicManagement.IntegrationTests;

/// <summary>
/// Covers the tamper-evident trail that must record every access to, or change of, patient medical
/// data.
/// </summary>
[Collection(ClinicApiCollection.Name)]
public sealed class AuditTrailTests
{
    private readonly ClinicApiFactory _factory;

    public AuditTrailTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task RecordAsync_AppendsHashChainedEntriesForPatientMedicalData()
    {
        var entityId = Guid.NewGuid().ToString("N");
        var patientId = Guid.NewGuid();

        using (var scope = _factory.CreateScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

            await audit.RecordAsync(
                AuditAction.Viewed,
                "PatientVisit",
                entityId,
                patientId,
                changedFieldNames: ["Diagnosis", "Notes"]);

            await audit.RecordAsync(
                AuditAction.Updated,
                "PatientVisit",
                entityId,
                patientId,
                changedFieldNames: ["Diagnosis"]);
        }

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var entries = await context.AuditLogEntries
                .Where(entry => entry.EntityId == entityId)
                .OrderBy(entry => entry.Id)
                .ToArrayAsync();

            Assert.Equal(2, entries.Length);

            var first = entries[0];
            var second = entries[1];

            Assert.Equal(AuditAction.Viewed, first.Action);
            Assert.Equal("PatientVisit", first.EntityName);
            Assert.Equal(patientId, first.PatientId);
            Assert.Equal("Diagnosis,Notes", first.ChangedFields);

            // A fresh log starts with no predecessor; a running one links to the entry before it.
            // Either way an entry can never be its own predecessor.
            Assert.NotEqual(first.EntryHash, first.PreviousHash);
            Assert.False(string.IsNullOrWhiteSpace(first.EntryHash));

            Assert.Equal(first.EntryHash, second.PreviousHash);
            Assert.Equal(AuditAction.Updated, second.Action);
            Assert.Equal("Diagnosis", second.ChangedFields);
            Assert.NotEqual(first.EntryHash, second.EntryHash);
        }
    }

    [RequiresDockerFact]
    public async Task RecordAsync_ChainsEveryEntryWrittenAfterIt()
    {
        var entityId = Guid.NewGuid().ToString("N");

        using (var scope = _factory.CreateScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

            for (var index = 0; index < 5; index++)
            {
                await audit.RecordAsync(
                    index is 0 ? AuditAction.Created : AuditAction.Updated,
                    "Patient",
                    entityId,
                    null,
                    changedFieldNames: [$"Field{index}"]);
            }
        }

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var entries = await context.AuditLogEntries
                .Where(entry => entry.EntityId == entityId)
                .OrderBy(entry => entry.Id)
                .ToArrayAsync();

            Assert.Equal(5, entries.Length);

            for (var index = 1; index < entries.Length; index++)
            {
                Assert.Equal(entries[index - 1].EntryHash, entries[index].PreviousHash);
            }

            Assert.Equal(
                entries.Select(entry => entry.EntryHash).Distinct(StringComparer.Ordinal).Count(),
                entries.Length);
        }
    }

    [RequiresDockerFact]
    public async Task RecordAsync_IsRegisteredAlongsideTheHttpContextAccessor()
    {
        using var scope = _factory.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAuditService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());
    }
}
