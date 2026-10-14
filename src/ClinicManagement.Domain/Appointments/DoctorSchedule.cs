using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Users;

namespace ClinicManagement.Domain.Appointments;

public sealed class DoctorSchedule
{
    private DoctorSchedule()
    {
    }

    public Guid Id { get; private set; }

    public Guid DoctorId { get; private set; }

    public Doctor? Doctor { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly StartsAt { get; private set; }

    public TimeOnly EndsAt { get; private set; }

    public int SlotMinutes { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static DoctorSchedule Create(
        Guid doctorId,
        DayOfWeek dayOfWeek,
        TimeOnly startsAt,
        TimeOnly endsAt,
        int slotMinutes,
        DateTime nowUtc)
    {
        if (doctorId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "doctorId is required.");
        }

        if (!Enum.IsDefined(dayOfWeek))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "dayOfWeek is invalid.");
        }

        if (endsAt <= startsAt)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "endsAt must be after startsAt.");
        }

        if (slotMinutes is < 5 or > 480)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "slotMinutes must be between 5 and 480.");
        }

        return new DoctorSchedule
        {
            DoctorId = doctorId,
            DayOfWeek = dayOfWeek,
            StartsAt = startsAt,
            EndsAt = endsAt,
            SlotMinutes = slotMinutes,
            IsActive = true,
            CreatedAtUtc = nowUtc
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public bool Covers(DateTime startsAtUtc, DateTime endsAtUtc)
    {
        var day = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc).DayOfWeek;
        if (day != DayOfWeek)
        {
            return false;
        }

        var start = TimeOnly.FromDateTime(startsAtUtc);
        var end = TimeOnly.FromDateTime(endsAtUtc);
        return start >= StartsAt && end <= EndsAt;
    }
}