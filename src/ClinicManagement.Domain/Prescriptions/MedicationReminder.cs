using ClinicManagement.Domain.Common;

namespace ClinicManagement.Domain.Prescriptions;

public sealed class MedicationReminder
{
    private MedicationReminder()
    {
    }

    public Guid Id { get; private set; }

    public Guid PrescriptionId { get; private set; }

    public Prescription? Prescription { get; private set; }

    public Guid PrescriptionItemId { get; private set; }

    public PrescriptionItem? PrescriptionItem { get; private set; }

    public Guid PatientId { get; private set; }

    public Patients.Patient? Patient { get; private set; }

    public DateTime ScheduledForUtc { get; private set; }

    public ReminderStatus Status { get; private set; }

    public DateTime? DeliveredAtUtc { get; private set; }

    public string? FailureReason { get; private set; }

    public static MedicationReminder Schedule(
        Guid prescriptionId,
        Guid prescriptionItemId,
        Guid patientId,
        DateTime scheduledForUtc,
        DateTime nowUtc)
    {
        if (prescriptionId == Guid.Empty || prescriptionItemId == Guid.Empty || patientId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "prescriptionId, prescriptionItemId and patientId are required.");
        }

        if (scheduledForUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "scheduledForUtc must be UTC.");
        }

        return new MedicationReminder
        {
            PrescriptionId = prescriptionId,
            PrescriptionItemId = prescriptionItemId,
            PatientId = patientId,
            ScheduledForUtc = scheduledForUtc,
            Status = ReminderStatus.Scheduled
        };
    }

    public void MarkDelivered(DateTime nowUtc)
    {
        if (Status != ReminderStatus.Scheduled)
        {
            throw new DomainException(ErrorCodes.InvalidStateTransition, $"Reminder cannot be delivered from '{Status}'.");
        }

        Status = ReminderStatus.Delivered;
        DeliveredAtUtc = nowUtc;
    }

    public void MarkFailed(string? reason, DateTime nowUtc)
    {
        if (Status != ReminderStatus.Scheduled)
        {
            throw new DomainException(ErrorCodes.InvalidStateTransition, $"Reminder cannot fail from '{Status}'.");
        }

        Status = ReminderStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason.Trim();
        DeliveredAtUtc = nowUtc;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status != ReminderStatus.Scheduled)
        {
            throw new DomainException(ErrorCodes.InvalidStateTransition, $"Reminder cannot be cancelled from '{Status}'.");
        }

        Status = ReminderStatus.Cancelled;
        DeliveredAtUtc = nowUtc;
    }
}