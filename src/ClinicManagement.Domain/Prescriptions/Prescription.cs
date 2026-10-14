using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Users;

namespace ClinicManagement.Domain.Prescriptions;

public enum ReminderStatus
{
    Scheduled = 1,
    Delivered = 2,
    Failed = 3,
    Cancelled = 4
}

public enum AllergyCheckOutcome
{
    NotChecked = 1,
    NoKnownAllergy = 2,
    MatchedAllergy = 3
}

public sealed class Prescription
{
    private readonly List<PrescriptionItem> _items = [];

    private Prescription()
    {
    }

    public Guid Id { get; private set; }

    public Guid PatientId { get; private set; }

    public Patients.Patient? Patient { get; private set; }

    public Guid DoctorId { get; private set; }

    public Doctor? Doctor { get; private set; }

    public Guid? VisitId { get; private set; }

    public Patients.PatientVisit? Visit { get; private set; }

    public DateTime IssuedAtUtc { get; private set; }

    [EncryptedColumn]
    public string? Notes { get; private set; }

    public IReadOnlyCollection<PrescriptionItem> Items => _items;

    public static Prescription Create(
        Guid patientId,
        Guid doctorId,
        Guid? visitId,
        string? notes,
        DateTime nowUtc)
    {
        if (patientId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "patientId is required.");
        }

        if (doctorId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "doctorId is required.");
        }

        return new Prescription
        {
            PatientId = patientId,
            DoctorId = doctorId,
            VisitId = visitId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            IssuedAtUtc = nowUtc
        };
    }

    public PrescriptionItem AddItem(
        string drugName,
        string dose,
        string? route,
        int timesPerDay,
        int durationDays,
        string? instructions,
        IEnumerable<DoseTime>? doseTimes,
        AllergyCheckOutcome allergyCheckOutcome,
        Guid? matchedAllergyId,
        DateTime nowUtc)
    {
        var item = PrescriptionItem.Create(
            Id,
            drugName,
            dose,
            route,
            timesPerDay,
            durationDays,
            instructions,
            doseTimes,
            allergyCheckOutcome,
            matchedAllergyId,
            nowUtc);

        _items.Add(item);
        return item;
    }
}