using System.Buffers.Binary;
using System.Security.Cryptography;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;

namespace ClinicManagement.Domain.Appointments;

public sealed class Appointment
{
    private Appointment()
    {
    }

    public Guid Id { get; private set; }

    public Guid PatientId { get; private set; }

    public Patient? Patient { get; private set; }

    public Guid DoctorId { get; private set; }

    public Doctor? Doctor { get; private set; }

    public DateTime StartsAtUtc { get; private set; }

    public DateTime EndsAtUtc { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public string? ReasonForVisit { get; private set; }

    public Guid BookedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? ConfirmedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTime? NoShowAtUtc { get; private set; }

    public Guid Version { get; private set; }

    public long? SlotKey { get; private set; }

    public bool BlocksTheSlot => Status is AppointmentStatus.Booked or AppointmentStatus.Confirmed;

    public static long ComputeSlotKey(Guid doctorId, DateTime startsAtUtc)
    {
        Span<byte> buffer = stackalloc byte[24];
        doctorId.TryWriteBytes(buffer[..16]);
        BinaryPrimitives.WriteInt64LittleEndian(buffer[16..], new DateTimeOffset(DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc)).Ticks);

        var hash = SHA256.HashData(buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(hash.AsSpan(0, 8));
    }

    public static Appointment Book(
        Guid patientId,
        Guid doctorId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string? reasonForVisit,
        Guid bookedByUserId,
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

        if (bookedByUserId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "bookedByUserId is required.");
        }

        if (endsAtUtc <= startsAtUtc)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "endsAtUtc must be after startsAtUtc.");
        }

        if (startsAtUtc.Kind != DateTimeKind.Utc || endsAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "Appointment times must be UTC.");
        }

        return new Appointment
        {
            PatientId = patientId,
            DoctorId = doctorId,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            Status = AppointmentStatus.Booked,
            ReasonForVisit = string.IsNullOrWhiteSpace(reasonForVisit) ? null : reasonForVisit.Trim(),
            BookedByUserId = bookedByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = Guid.NewGuid(),
            SlotKey = ComputeSlotKey(doctorId, startsAtUtc)
        };
    }

    public void Confirm(DateTime nowUtc)
    {
        EnsureCurrentStatus(AppointmentStatus.Booked);
        Status = AppointmentStatus.Confirmed;
        ConfirmedAtUtc = nowUtc;
        Touch(nowUtc);
    }

    public void Complete(DateTime nowUtc)
    {
        EnsureCurrentStatus(AppointmentStatus.Booked, AppointmentStatus.Confirmed);
        Status = AppointmentStatus.Done;
        CompletedAtUtc = nowUtc;
        SlotKey = null;
        Touch(nowUtc);
    }

    public void MarkNoShow(DateTime nowUtc)
    {
        EnsureCurrentStatus(AppointmentStatus.Booked, AppointmentStatus.Confirmed);
        Status = AppointmentStatus.NoShow;
        NoShowAtUtc = nowUtc;
        SlotKey = null;
        Touch(nowUtc);
    }

    public void Cancel(string? reason, DateTime nowUtc)
    {
        EnsureCurrentStatus(AppointmentStatus.Booked, AppointmentStatus.Confirmed);
        Status = AppointmentStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        SlotKey = null;
        Touch(nowUtc);
    }

    public void Reschedule(DateTime newStartsAtUtc, DateTime newEndsAtUtc, DateTime nowUtc)
    {
        if (newEndsAtUtc <= newStartsAtUtc)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "newEndsAtUtc must be after newStartsAtUtc.");
        }

        if (newStartsAtUtc.Kind != DateTimeKind.Utc || newEndsAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "Appointment times must be UTC.");
        }

        EnsureCurrentStatus(AppointmentStatus.Booked, AppointmentStatus.Confirmed);

        StartsAtUtc = newStartsAtUtc;
        EndsAtUtc = newEndsAtUtc;
        SlotKey = ComputeSlotKey(DoctorId, newStartsAtUtc);
        Touch(nowUtc);
    }

    private void EnsureCurrentStatus(params AppointmentStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException(
                ErrorCodes.InvalidStateTransition,
                $"Appointment cannot move from '{Status}' to the requested state.");
        }
    }

    private void Touch(DateTime nowUtc)
    {
        UpdatedAtUtc = nowUtc;
        Version = Guid.NewGuid();
    }
}