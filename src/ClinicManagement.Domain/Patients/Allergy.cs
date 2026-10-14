using ClinicManagement.Domain.Common;

namespace ClinicManagement.Domain.Patients;

public enum AllergySeverity
{
    Unknown = 1,
    Mild = 2,
    Moderate = 3,
    Severe = 4
}

public sealed class Allergy
{
    private Allergy()
    {
    }

    public Guid Id { get; private set; }

    public Guid PatientId { get; private set; }

    public Patient? Patient { get; private set; }

    public string SubstanceName { get; private set; } = string.Empty;

    public string NormalizedSubstance { get; private set; } = string.Empty;

    [EncryptedColumn]
    public string? Reaction { get; private set; }

    public AllergySeverity Severity { get; private set; } = AllergySeverity.Unknown;

    [EncryptedColumn]
    public string? Notes { get; private set; }

    public Guid RecordedByUserId { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public static Allergy Record(
        Guid patientId,
        string substanceName,
        string? reaction,
        AllergySeverity severity,
        string? notes,
        Guid recordedByUserId,
        DateTime nowUtc)
    {
        if (patientId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "patientId is required.");
        }

        var name = substanceName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "substanceName is required.");
        }

        return new Allergy
        {
            PatientId = patientId,
            SubstanceName = name,
            NormalizedSubstance = SubstanceNormalizer.Normalize(name),
            Reaction = string.IsNullOrWhiteSpace(reaction) ? null : reaction.Trim(),
            Severity = severity,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            RecordedByUserId = recordedByUserId,
            RecordedAtUtc = nowUtc
        };
    }
}