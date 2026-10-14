using ClinicManagement.Domain.Common;

namespace ClinicManagement.Domain.Patients;

public sealed class MedicalHistoryEntry
{
    private MedicalHistoryEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid PatientId { get; private set; }

    public Patient? Patient { get; private set; }

    public string ConditionName { get; private set; } = string.Empty;

    public string NormalizedCondition { get; private set; } = string.Empty;

    public DateOnly? DiagnosedOn { get; private set; }

    public bool IsActive { get; private set; } = true;

    [EncryptedColumn]
    public string? Notes { get; private set; }

    public Guid RecordedByUserId { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public static MedicalHistoryEntry Record(
        Guid patientId,
        string conditionName,
        DateOnly? diagnosedOn,
        string? notes,
        Guid recordedByUserId,
        DateTime nowUtc)
    {
        if (patientId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "patientId is required.");
        }

        var name = conditionName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "conditionName is required.");
        }

        return new MedicalHistoryEntry
        {
            PatientId = patientId,
            ConditionName = name,
            NormalizedCondition = SubstanceNormalizer.Normalize(name),
            DiagnosedOn = diagnosedOn,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            RecordedByUserId = recordedByUserId,
            RecordedAtUtc = nowUtc
        };
    }

    public void Retire()
    {
        IsActive = false;
    }
}