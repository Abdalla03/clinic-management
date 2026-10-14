using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Domain.Appointments;

namespace ClinicManagement.Application.Appointments;

public sealed record AppointmentSummary(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    AppointmentStatus Status,
    string? ReasonForVisit,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record AppointmentDetails(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    AppointmentStatus Status,
    string? ReasonForVisit,
    Guid BookedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    DateTime? NoShowAtUtc,
    Guid Version);

public sealed record BookAppointmentRequest(
    Guid PatientId,
    Guid DoctorId,
    DateTime StartsAtUtc,
    int DurationMinutes = 30,
    string? ReasonForVisit = null);

public sealed record RescheduleAppointmentRequest(
    DateTime StartsAtUtc,
    int DurationMinutes = 30);

public sealed record CancelAppointmentRequest(string? Reason = null);

public sealed record AppointmentSearchQuery
{
    public Guid? DoctorId { get; init; }

    public Guid? PatientId { get; init; }

    public AppointmentStatus? Status { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
}

public sealed record AvailabilitySlot(DateTime StartsAtUtc, DateTime EndsAtUtc);

public sealed record DoctorSummary(Guid Id, string Name, bool IsAcceptingAppointments);

public sealed record DoctorScheduleResponse(
    Guid Id,
    Guid DoctorId,
    DayOfWeek DayOfWeek,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    int SlotMinutes,
    bool IsActive);

public sealed record UpsertDoctorScheduleRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    int SlotMinutes = 30);
