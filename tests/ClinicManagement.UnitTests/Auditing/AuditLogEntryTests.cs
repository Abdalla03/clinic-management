using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Common;
using Xunit;

namespace ClinicManagement.UnitTests.Auditing;

public sealed class AuditLogEntryTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PatientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime OccurredAt = new(2026, 3, 14, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_RequiresAnEntityName()
    {
        var exception = Assert.Throws<DomainException>(() => AuditLogEntry.Create(
            OccurredAt, ActorId, "DOCTOR", AuditAction.Updated, entityName: "  ", null, PatientId, null, null, null));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public void Create_CarriesThePreviousHashForwardSoTheChainIsLinkable()
    {
        var entry = AuditLogEntry.Create(
            OccurredAt, ActorId, "DOCTOR", AuditAction.Viewed, "PatientVisit", "visit-1", PatientId, "corr-1", null, "PREVIOUS");

        Assert.Equal("PREVIOUS", entry.PreviousHash);
        Assert.Equal(string.Empty, entry.EntryHash);
        Assert.Equal(OccurredAt, entry.OccurredAtUtc);
        Assert.Equal(AuditAction.Viewed, entry.Action);
        Assert.Equal("PatientVisit", entry.EntityName);
        Assert.Equal("visit-1", entry.EntityId);
        Assert.Equal(PatientId, entry.PatientId);
        Assert.Equal("corr-1", entry.CorrelationId);
        Assert.Null(entry.ChangedFields);
    }

    [Fact]
    public void Create_NormalisesChangedFieldNamesToASortedCommaSeparatedList()
    {
        var entry = AuditLogEntry.Create(
            OccurredAt, ActorId, null, AuditAction.Updated, "Patient", null, null, null,
            changedFieldNames: ["Notes", "Address", "Notes"], previousHash: null);

        Assert.Equal("Address,Notes", entry.ChangedFields);
    }

    [Fact]
    public void Create_TreatsAnEmptyChangedFieldSetAsNoChangesAtAll()
    {
        var entry = AuditLogEntry.Create(
            OccurredAt, null, null, AuditAction.Updated, "Patient", null, null, null,
            changedFieldNames: [], previousHash: null);

        Assert.Null(entry.ChangedFields);
        Assert.Null(entry.ActorUserId);
        Assert.Null(entry.ActorRole);
    }

    [Fact]
    public void SealWithHash_MakesTheEntryHashPermanent()
    {
        var entry = AuditLogEntry.Create(
            OccurredAt, ActorId, "DOCTOR", AuditAction.Created, "Patient", null, null, null, null, null);

        entry.SealWithHash("ABC123");

        Assert.Equal("ABC123", entry.EntryHash);
    }
}
