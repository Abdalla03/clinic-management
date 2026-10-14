namespace ClinicManagement.Domain.Auditing;

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    Viewed = 4,
    LoginSucceeded = 5,
    LoginFailed = 6,
    MfaEnabled = 7,
    MfaVerified = 8,
    MfaDisabled = 9,
    Exported = 10,
    ConsentGranted = 11,
    ConsentRevoked = 12
}

public sealed class AuditLogEntry
{
    private AuditLogEntry()
    {
    }

    public long Id { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string? ActorRole { get; private set; }

    public AuditAction Action { get; private set; }

    public string EntityName { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public Guid? PatientId { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? ChangedFields { get; private set; }

    public string EntryHash { get; private set; } = string.Empty;

    public string? PreviousHash { get; private set; }

    public static AuditLogEntry Create(
        DateTime occurredAtUtc,
        Guid? actorUserId,
        string? actorRole,
        AuditAction action,
        string entityName,
        string? entityId,
        Guid? patientId,
        string? correlationId,
        IReadOnlyCollection<string>? changedFieldNames,
        string? previousHash)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new Common.DomainException(Common.ErrorCodes.ValidationFailed, "entityName is required.");
        }

        var changedFields = changedFieldNames is { Count: > 0 }
            ? string.Join(',', changedFieldNames.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            : null;

        return new AuditLogEntry
        {
            OccurredAtUtc = occurredAtUtc,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            PatientId = patientId,
            CorrelationId = correlationId,
            ChangedFields = changedFields,
            PreviousHash = previousHash
        };
    }

    public void SealWithHash(string entryHash)
    {
        EntryHash = entryHash;
    }
}