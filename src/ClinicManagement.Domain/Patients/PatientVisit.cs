using ClinicManagement.Domain.Common;

namespace ClinicManagement.Domain.Patients;

public sealed class PatientVisit
{
    private PatientVisit()
    {
    }

    public Guid Id { get; private set; }

    public Guid PatientId { get; private set; }

    public Patient? Patient { get; private set; }

    public Guid DoctorId { get; private set; }

    public Users.Doctor? Doctor { get; private set; }

    public Guid? AppointmentId { get; private set; }

    public Appointments.Appointment? Appointment { get; private set; }

    public DateTime VisitAtUtc { get; private set; }

    [EncryptedColumn]
    public string? ChiefComplaint { get; private set; }

    [EncryptedColumn]
    public string? Notes { get; private set; }

    [EncryptedColumn]
    public string? Diagnosis { get; private set; }

    public Guid RecordedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static PatientVisit Record(
        Guid patientId,
        Guid doctorId,
        Guid? appointmentId,
        DateTime visitAtUtc,
        string? chiefComplaint,
        string? notes,
        string? diagnosis,
        Guid recordedByUserId,
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

        return new PatientVisit
        {
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentId = appointmentId,
            VisitAtUtc = visitAtUtc,
            ChiefComplaint = Trim(chiefComplaint),
            Notes = Trim(notes),
            Diagnosis = Trim(diagnosis),
            RecordedByUserId = recordedByUserId,
            CreatedAtUtc = nowUtc
        };
    }

    public void UpdateClinicalContent(string? chiefComplaint, string? notes, string? diagnosis, DateTime nowUtc)
    {
        ChiefComplaint = Trim(chiefComplaint);
        Notes = Trim(notes);
        Diagnosis = Trim(diagnosis);
        CreatedAtUtc = nowUtc;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}