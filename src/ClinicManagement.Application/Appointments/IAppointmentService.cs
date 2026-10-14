using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Paging;

namespace ClinicManagement.Application.Appointments;

public interface IAppointmentService
{
    Task<Result<PagedResult<AppointmentSummary>>> SearchAppointmentsAsync(
        AppointmentSearchQuery query,
        CancellationToken cancellationToken);

    Task<Result<AppointmentDetails>> GetAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<Result<AppointmentDetails>> BookAppointmentAsync(
        BookAppointmentRequest request,
        CancellationToken cancellationToken);

    Task<Result<AppointmentDetails>> RescheduleAppointmentAsync(
        Guid appointmentId,
        RescheduleAppointmentRequest request,
        CancellationToken cancellationToken);

    Task<Result> ConfirmAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<Result> CompleteAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<Result> MarkNoShowAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<Result> CancelAppointmentAsync(
        Guid appointmentId,
        CancelAppointmentRequest request,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<AppointmentSummary>>> GetOwnAppointmentsAsync(
        PageRequest page,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<AvailabilitySlot>>> GetAvailabilityAsync(
        Guid doctorId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DoctorSummary>>> ListDoctorsAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DoctorScheduleResponse>>> GetScheduleAsync(
        Guid doctorId,
        CancellationToken cancellationToken);

    Task<Result<DoctorScheduleResponse>> AddScheduleAsync(
        Guid doctorId,
        UpsertDoctorScheduleRequest request,
        CancellationToken cancellationToken);

    Task<Result> DeactivateScheduleAsync(
        Guid doctorId,
        Guid scheduleId,
        CancellationToken cancellationToken);
}
