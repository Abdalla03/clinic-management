using System.Security.Cryptography;
using System.Text;
using ClinicManagement.Application.Abstractions;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagement.Infrastructure.Auditing;

public sealed class AuditService(
    ClinicDbContext dbContext,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor) : IAuditService
{
    public async Task RecordAsync(
        AuditAction action,
        string entityName,
        string? entityId = null,
        Guid? patientId = null,
        IReadOnlyCollection<string>? changedFieldNames = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        var previousHash = await dbContext.AuditLogEntries
            .OrderByDescending(entry => entry.Id)
            .Select(entry => entry.EntryHash)
            .FirstOrDefaultAsync(cancellationToken);

        var occurredAtUtc = DateTime.UtcNow;
        var entry = AuditLogEntry.Create(
            occurredAtUtc,
            currentUser.UserId,
            currentUser.Roles.FirstOrDefault(),
            action,
            entityName,
            entityId,
            patientId,
            httpContextAccessor.HttpContext?.TraceIdentifier,
            changedFieldNames,
            previousHash);

        entry.SealWithHash(ComputeHash(previousHash, entry));

        dbContext.AuditLogEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ComputeHash(string? previousHash, AuditLogEntry entry)
    {
        var canonical = string.Join(
            '|',
            previousHash,
            entry.OccurredAtUtc.Ticks,
            entry.ActorUserId,
            entry.ActorRole,
            entry.Action,
            entry.EntityName,
            entry.EntityId,
            entry.PatientId,
            entry.CorrelationId,
            entry.ChangedFields);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
