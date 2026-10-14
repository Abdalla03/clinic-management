using ClinicManagement.Domain.Auditing;

namespace ClinicManagement.Application.Abstractions;

public interface IAuditService
{
    /// <summary>
    /// Appends a hash-chained audit entry for an access to, or a change of, patient medical data.
    /// The caller is responsible for persisting the domain change first so the entry can reference
    /// the persisted identifier.
    /// </summary>
    Task RecordAsync(
        AuditAction action,
        string entityName,
        string? entityId = null,
        Guid? patientId = null,
        IReadOnlyCollection<string>? changedFieldNames = null,
        CancellationToken cancellationToken = default);
}
