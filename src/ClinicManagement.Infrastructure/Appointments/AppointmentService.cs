using ClinicManagement.Application.Abstractions;
using ClinicManagement.Application.Appointments;
using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Extensions;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Domain.Appointments;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClinicManagement.Infrastructure.Appointments;

public sealed class AppointmentService(
    ClinicDbContext dbContext,
    ICurrentUser currentUser,
    IAuditService auditService,
    TimeProvider timeProvider,
    IValidator<BookAppointmentRequest> bookValidator,
    IValidator<RescheduleAppointmentRequest> rescheduleValidator,
    IValidator<CancelAppointmentRequest> cancelValidator,
    IValidator<AppointmentSearchQuery> searchValidator,
    IValidator<UpsertDoctorScheduleRequest> scheduleValidator,
    IValidator<PageRequest> pageValidator) : IAppointmentService
{
    private const int MaxAvailabilityRangeDays = 31;

    public async Task<Result<PagedResult<AppointmentSummary>>> SearchAppointmentsAsync(
        AppointmentSearchQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await searchValidator.ValidateAndBindAsync(query, cancellationToken);
        if (validation.Failed)
        {
            return Failure<PagedResult<AppointmentSummary>>(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<PagedResult<AppointmentSummary>>(
                ErrorCodes.Forbidden,
                "Patients may only view their own appointments.");
        }

        var page = new PageRequest { Page = query.Page, PageSize = query.PageSize };

        var appointments = dbContext.Appointments.AsNoTracking();

        if (query.DoctorId is { } doctorId)
        {
            appointments = appointments.Where(appointment => appointment.DoctorId == doctorId);
        }

        if (query.PatientId is { } patientId)
        {
            appointments = appointments.Where(appointment => appointment.PatientId == patientId);
        }

        if (query.Status is { } status)
        {
            appointments = appointments.Where(appointment => appointment.Status == status);
        }

        if (query.FromUtc is { } fromUtc)
        {
            appointments = appointments.Where(appointment => appointment.EndsAtUtc > fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            appointments = appointments.Where(appointment => appointment.StartsAtUtc < toUtc);
        }

        var totalCount = await appointments.CountAsync(cancellationToken);

        var items = await appointments
            .Include(appointment => appointment.Patient)
            .OrderBy(appointment => appointment.StartsAtUtc)
            .ThenBy(appointment => appointment.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        var summaries = (await MapSummariesAsync(items, cancellationToken)).Select(ToSummary).ToArray();

        return Result<PagedResult<AppointmentSummary>>.Success(
            PagedResult<AppointmentSummary>.Create(summaries, page, totalCount));
    }

    public async Task<Result<AppointmentDetails>> GetAppointmentAsync(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<AppointmentDetails>(
                ErrorCodes.Forbidden,
                "Patients may only view their own appointments.");
        }

        var appointment = await dbContext.Appointments
            .AsNoTracking()
            .Include(candidate => candidate.Patient)
            .FirstOrDefaultAsync(candidate => candidate.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return Failure<AppointmentDetails>(ErrorCodes.NotFound, "Appointment was not found.");
        }

        await auditService.RecordAsync(
            AuditAction.Viewed,
            nameof(Appointment),
            appointment.Id.ToString(),
            appointment.PatientId,
            cancellationToken: cancellationToken);

        return Result<AppointmentDetails>.Success((await MapSummariesAsync([appointment], cancellationToken))[0]);
    }

    public async Task<Result<AppointmentDetails>> BookAppointmentAsync(
        BookAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await bookValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Failure<AppointmentDetails>(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<AppointmentDetails>(ErrorCodes.Forbidden, "Patients cannot book appointments.");
        }

        if (currentUser.UserId is not { } bookedByUserId)
        {
            return Failure<AppointmentDetails>(ErrorCodes.Forbidden, "No signed-in user was found.");
        }

        var startsAtUtc = NormalizeUtc(request.StartsAtUtc);
        var endsAtUtc = startsAtUtc.AddMinutes(request.DurationMinutes);

        var slot = await ValidateSlotAsync(
            request.DoctorId,
            request.PatientId,
            startsAtUtc,
            endsAtUtc,
            excludedAppointmentId: null,
            cancellationToken);

        if (slot.Failed)
        {
            return Failure<AppointmentDetails>(slot.Error!);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var appointment = Appointment.Book(
            request.PatientId,
            request.DoctorId,
            startsAtUtc,
            endsAtUtc,
            request.ReasonForVisit,
            bookedByUserId,
            now);

        dbContext.Appointments.Add(appointment);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Failure<AppointmentDetails>(
                ErrorCodes.AppointmentSlotTaken,
                "That slot was just taken for this doctor. Pick another time.");
        }

        await auditService.RecordAsync(
            AuditAction.Created,
            nameof(Appointment),
            appointment.Id.ToString(),
            appointment.PatientId,
            cancellationToken: cancellationToken);

        var created = await dbContext.Appointments
            .Include(candidate => candidate.Patient)
            .SingleAsync(candidate => candidate.Id == appointment.Id, cancellationToken);

        return Result<AppointmentDetails>.Success((await MapSummariesAsync([created], cancellationToken))[0]);
    }

    public async Task<Result<AppointmentDetails>> RescheduleAppointmentAsync(
        Guid appointmentId,
        RescheduleAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await rescheduleValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Failure<AppointmentDetails>(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<AppointmentDetails>(ErrorCodes.Forbidden, "Patients cannot reschedule appointments.");
        }

        var appointment = await dbContext.Appointments
            .Include(candidate => candidate.Patient)
            .FirstOrDefaultAsync(candidate => candidate.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return Failure<AppointmentDetails>(ErrorCodes.NotFound, "Appointment was not found.");
        }

        var startsAtUtc = NormalizeUtc(request.StartsAtUtc);
        var endsAtUtc = startsAtUtc.AddMinutes(request.DurationMinutes);

        var slot = await ValidateSlotAsync(
            appointment.DoctorId,
            appointment.PatientId,
            startsAtUtc,
            endsAtUtc,
            appointment.Id,
            cancellationToken);

        if (slot.Failed)
        {
            return Failure<AppointmentDetails>(slot.Error!);
        }

        try
        {
            appointment.Reschedule(startsAtUtc, endsAtUtc, timeProvider.GetUtcNow().UtcDateTime);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException exception)
        {
            return Failure<AppointmentDetails>(exception.Code, exception.Message);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Failure<AppointmentDetails>(
                ErrorCodes.AppointmentSlotTaken,
                "That slot was just taken for this doctor. Pick another time.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure<AppointmentDetails>(
                ErrorCodes.ConcurrencyConflict,
                "The appointment was modified by someone else. Reload it and try again.");
        }

        await auditService.RecordAsync(
            AuditAction.Updated,
            nameof(Appointment),
            appointment.Id.ToString(),
            appointment.PatientId,
            [nameof(Appointment.StartsAtUtc), nameof(Appointment.EndsAtUtc)],
            cancellationToken);

        return Result<AppointmentDetails>.Success((await MapSummariesAsync([appointment], cancellationToken))[0]);
    }

    public Task<Result> ConfirmAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        TransitionAsync(
            appointmentId,
            "Confirm",
            appointment => appointment.Confirm(timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);

    public Task<Result> CompleteAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        TransitionAsync(
            appointmentId,
            "Complete",
            appointment => appointment.Complete(timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);

    public Task<Result> MarkNoShowAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        TransitionAsync(
            appointmentId,
            "MarkNoShow",
            appointment => appointment.MarkNoShow(timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);

    public async Task<Result> CancelAppointmentAsync(
        Guid appointmentId,
        CancelAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await cancelValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result.Failure(ErrorCodes.Forbidden, "Patients cannot cancel appointments.");
        }

        var appointment = await dbContext.Appointments
            .FirstOrDefaultAsync(candidate => candidate.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Appointment was not found.");
        }

        try
        {
            appointment.Cancel(request.Reason, timeProvider.GetUtcNow().UtcDateTime);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result.Failure(exception.Code, exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(
                ErrorCodes.ConcurrencyConflict,
                "The appointment was modified by someone else. Reload it and try again.");
        }

        await auditService.RecordAsync(
            AuditAction.Updated,
            nameof(Appointment),
            appointment.Id.ToString(),
            appointment.PatientId,
            [nameof(Appointment.Status), nameof(Appointment.CancellationReason)],
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<PagedResult<AppointmentSummary>>> GetOwnAppointmentsAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var pageValidation = await pageValidator.ValidateAndBindAsync(page, cancellationToken);
        if (pageValidation.Failed)
        {
            return Failure<PagedResult<AppointmentSummary>>(pageValidation.Error!);
        }

        var patientId = await ResolveOwnPatientIdAsync(cancellationToken);
        if (patientId == Guid.Empty)
        {
            return Failure<PagedResult<AppointmentSummary>>(
                ErrorCodes.NotFound,
                "No patient record is linked to this account.");
        }

        var appointments = dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.PatientId == patientId);

        var totalCount = await appointments.CountAsync(cancellationToken);

        var items = await appointments
            .Include(appointment => appointment.Patient)
            .OrderBy(appointment => appointment.StartsAtUtc)
            .ThenBy(appointment => appointment.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        var summaries = (await MapSummariesAsync(items, cancellationToken)).Select(ToSummary).ToArray();

        return Result<PagedResult<AppointmentSummary>>.Success(
            PagedResult<AppointmentSummary>.Create(summaries, page, totalCount));
    }

    public async Task<Result<IReadOnlyList<AvailabilitySlot>>> GetAvailabilityAsync(
        Guid doctorId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<IReadOnlyList<AvailabilitySlot>>(
                ErrorCodes.Forbidden,
                "Patients cannot inspect doctor availability.");
        }

        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);

        if (to <= from)
        {
            return Failure<IReadOnlyList<AvailabilitySlot>>(
                ErrorCodes.ValidationFailed,
                "toUtc must be after fromUtc.");
        }

        if ((to - from).TotalDays > MaxAvailabilityRangeDays)
        {
            return Failure<IReadOnlyList<AvailabilitySlot>>(
                ErrorCodes.ValidationFailed,
                $"Availability may only be requested for up to {MaxAvailabilityRangeDays} days.");
        }

        var doctor = await dbContext.Doctors.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == doctorId, cancellationToken);

        if (doctor is null)
        {
            return Failure<IReadOnlyList<AvailabilitySlot>>(ErrorCodes.NotFound, "Doctor was not found.");
        }

        var schedules = await dbContext.DoctorSchedules
            .AsNoTracking()
            .Where(schedule => schedule.DoctorId == doctorId && schedule.IsActive)
            .ToListAsync(cancellationToken);

        var busy = (await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.DoctorId == doctorId &&
                                  appointment.StartsAtUtc < to &&
                                  appointment.EndsAtUtc > from &&
                                  (appointment.Status == AppointmentStatus.Booked ||
                                   appointment.Status == AppointmentStatus.Confirmed))
            .Select(appointment => new { appointment.StartsAtUtc, appointment.EndsAtUtc })
            .ToListAsync(cancellationToken))
            .Select(appointment => (appointment.StartsAtUtc, appointment.EndsAtUtc))
            .ToArray();

        var slots = new List<AvailabilitySlot>();

        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            var dayStart = DateTime.SpecifyKind(day, DateTimeKind.Utc);

            foreach (var schedule in schedules.Where(candidate => candidate.DayOfWeek == dayStart.DayOfWeek))
            {
                var cursor = dayStart.Add(schedule.StartsAt.ToTimeSpan());
                var windowEndsAt = dayStart.Add(schedule.EndsAt.ToTimeSpan());

                while (cursor.AddMinutes(schedule.SlotMinutes) <= windowEndsAt)
                {
                    var slotEndsAt = cursor.AddMinutes(schedule.SlotMinutes);

                    if (cursor >= from && slotEndsAt <= to && !IsBusy(cursor, slotEndsAt, busy))
                    {
                        slots.Add(new AvailabilitySlot(cursor, slotEndsAt));
                    }

                    cursor = slotEndsAt;
                }
            }
        }

        return Result<IReadOnlyList<AvailabilitySlot>>.Success(
            slots.OrderBy(slot => slot.StartsAtUtc).ToArray());
    }

    public async Task<Result<IReadOnlyList<DoctorSummary>>> ListDoctorsAsync(CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<IReadOnlyList<DoctorSummary>>(
                ErrorCodes.Forbidden,
                "Patients cannot list doctors.");
        }

        var doctors = await dbContext.Doctors
            .AsNoTracking()
            .Join(
                dbContext.Users,
                doctor => doctor.ApplicationUserId,
                user => user.Id,
                (doctor, user) => new { doctor.Id, user.FullName, doctor.IsAcceptingAppointments })
            .OrderBy(pair => pair.FullName)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DoctorSummary>>.Success(
            doctors.Select(pair => new DoctorSummary(pair.Id, pair.FullName, pair.IsAcceptingAppointments)).ToArray());
    }

    public async Task<Result<IReadOnlyList<DoctorScheduleResponse>>> GetScheduleAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Doctors.AnyAsync(doctor => doctor.Id == doctorId, cancellationToken))
        {
            return Failure<IReadOnlyList<DoctorScheduleResponse>>(ErrorCodes.NotFound, "Doctor was not found.");
        }

        var schedules = await dbContext.DoctorSchedules
            .AsNoTracking()
            .Where(schedule => schedule.DoctorId == doctorId)
            .OrderBy(schedule => schedule.DayOfWeek)
            .ThenBy(schedule => schedule.StartsAt)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DoctorScheduleResponse>>.Success(schedules.Select(Map).ToArray());
    }

    public async Task<Result<DoctorScheduleResponse>> AddScheduleAsync(
        Guid doctorId,
        UpsertDoctorScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await scheduleValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Failure<DoctorScheduleResponse>(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<DoctorScheduleResponse>(ErrorCodes.Forbidden, "Patients cannot manage schedules.");
        }

        if (!await dbContext.Doctors.AnyAsync(doctor => doctor.Id == doctorId, cancellationToken))
        {
            return Failure<DoctorScheduleResponse>(ErrorCodes.NotFound, "Doctor was not found.");
        }

        var duplicate = await dbContext.DoctorSchedules.AnyAsync(
            schedule => schedule.DoctorId == doctorId &&
                        schedule.DayOfWeek == request.DayOfWeek &&
                        schedule.StartsAt == request.StartsAt,
            cancellationToken);

        if (duplicate)
        {
            return Failure<DoctorScheduleResponse>(
                ErrorCodes.Conflict,
                "The doctor already has a working-hours window that starts at that time on that day.");
        }

        var schedule = DoctorSchedule.Create(
            doctorId,
            request.DayOfWeek,
            request.StartsAt,
            request.EndsAt,
            request.SlotMinutes,
            timeProvider.GetUtcNow().UtcDateTime);

        dbContext.DoctorSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<DoctorScheduleResponse>.Success(Map(schedule));
    }

    public async Task<Result> DeactivateScheduleAsync(
        Guid doctorId,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result.Failure(ErrorCodes.Forbidden, "Patients cannot manage schedules.");
        }

        var schedule = await dbContext.DoctorSchedules
            .FirstOrDefaultAsync(candidate => candidate.Id == scheduleId && candidate.DoctorId == doctorId,
                cancellationToken);

        if (schedule is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Working-hours window was not found.");
        }

        if (!schedule.IsActive)
        {
            return Result.Failure(ErrorCodes.Conflict, "Working-hours window is already inactive.");
        }

        schedule.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ValidateSlotAsync(
        Guid doctorId,
        Guid patientId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        Guid? excludedAppointmentId,
        CancellationToken cancellationToken)
    {
        var doctor = await dbContext.Doctors.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == doctorId, cancellationToken);

        if (doctor is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Doctor was not found.");
        }

        if (!doctor.IsAcceptingAppointments)
        {
            return Result.Failure(ErrorCodes.Conflict, "The doctor is not accepting appointments right now.");
        }

        var patient = await dbContext.Patients.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == patientId, cancellationToken);

        if (patient is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Patient was not found.");
        }

        if (!patient.IsActive)
        {
            return Result.Failure(ErrorCodes.Conflict, "The patient record is inactive.");
        }

        var covered = await dbContext.DoctorSchedules
            .AsNoTracking()
            .AnyAsync(
                schedule => schedule.DoctorId == doctorId &&
                            schedule.IsActive &&
                            schedule.DayOfWeek == startsAtUtc.DayOfWeek &&
                            schedule.StartsAt <= TimeOnly.FromDateTime(startsAtUtc) &&
                            schedule.EndsAt >= TimeOnly.FromDateTime(endsAtUtc),
                cancellationToken);

        if (!covered)
        {
            return Result.Failure(
                ErrorCodes.AppointmentOutsideWorkingHours,
                "The requested time is outside the doctor's working hours.");
        }

        var doctorClash = await dbContext.Appointments.AsNoTracking().AnyAsync(
            appointment => appointment.DoctorId == doctorId &&
                           appointment.StartsAtUtc < endsAtUtc &&
                           appointment.EndsAtUtc > startsAtUtc &&
                           (appointment.Status == AppointmentStatus.Booked ||
                            appointment.Status == AppointmentStatus.Confirmed) &&
                           (excludedAppointmentId == null || appointment.Id != excludedAppointmentId),
            cancellationToken);

        if (doctorClash)
        {
            return Result.Failure(
                ErrorCodes.AppointmentOverlap,
                "The doctor already has an appointment that overlaps the requested time.");
        }

        var patientClash = await dbContext.Appointments.AsNoTracking().AnyAsync(
            appointment => appointment.PatientId == patientId &&
                           appointment.StartsAtUtc < endsAtUtc &&
                           appointment.EndsAtUtc > startsAtUtc &&
                           (appointment.Status == AppointmentStatus.Booked ||
                            appointment.Status == AppointmentStatus.Confirmed) &&
                           (excludedAppointmentId == null || appointment.Id != excludedAppointmentId),
            cancellationToken);

        if (patientClash)
        {
            return Result.Failure(
                ErrorCodes.AppointmentOverlap,
                "The patient already has an appointment that overlaps the requested time.");
        }

        return Result.Success();
    }

    private async Task<Result> TransitionAsync(
        Guid appointmentId,
        string transitionName,
        Action<Appointment> apply,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result.Failure(ErrorCodes.Forbidden, "Patients cannot change appointment status.");
        }

        var appointment = await dbContext.Appointments
            .FirstOrDefaultAsync(candidate => candidate.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Appointment was not found.");
        }

        try
        {
            apply(appointment);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result.Failure(exception.Code, exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(
                ErrorCodes.ConcurrencyConflict,
                "The appointment was modified by someone else. Reload it and try again.");
        }

        await auditService.RecordAsync(
            AuditAction.Updated,
            nameof(Appointment),
            appointment.Id.ToString(),
            appointment.PatientId,
            [transitionName],
            cancellationToken);

        return Result.Success();
    }

    private async Task<IReadOnlyList<AppointmentDetails>> MapSummariesAsync(
        IReadOnlyCollection<Appointment> appointments,
        CancellationToken cancellationToken)
    {
        if (appointments.Count == 0)
        {
            return [];
        }

        var doctorIds = appointments.Select(appointment => appointment.DoctorId).Distinct().ToArray();

        var doctorNames = await dbContext.Doctors
            .AsNoTracking()
            .Where(doctor => doctorIds.Contains(doctor.Id))
            .Join(
                dbContext.Users,
                doctor => doctor.ApplicationUserId,
                user => user.Id,
                (doctor, user) => new { doctor.Id, user.FullName })
            .ToDictionaryAsync(pair => pair.Id, pair => pair.FullName, cancellationToken);

        return appointments
            .Select(appointment => new AppointmentDetails(
                appointment.Id,
                appointment.PatientId,
                appointment.Patient?.FullName ?? string.Empty,
                appointment.DoctorId,
                doctorNames.GetValueOrDefault(appointment.DoctorId, string.Empty),
                appointment.StartsAtUtc,
                appointment.EndsAtUtc,
                appointment.Status,
                appointment.ReasonForVisit,
                appointment.BookedByUserId,
                appointment.CreatedAtUtc,
                appointment.UpdatedAtUtc,
                appointment.ConfirmedAtUtc,
                appointment.CompletedAtUtc,
                appointment.CancelledAtUtc,
                appointment.CancellationReason,
                appointment.NoShowAtUtc,
                appointment.Version))
            .ToArray();
    }

    private async Task<Guid> ResolveOwnPatientIdAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Guid.Empty;
        }

        return await dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.ApplicationUserId == userId)
            .Select(patient => patient.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool IsBusy(
        DateTime slotStart,
        DateTime slotEnd,
        IReadOnlyCollection<(DateTime StartsAtUtc, DateTime EndsAtUtc)> busy) =>
        busy.Any(taken => taken.StartsAtUtc < slotEnd && taken.EndsAtUtc > slotStart);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static AppointmentSummary ToSummary(AppointmentDetails details) => new(
        details.Id,
        details.PatientId,
        details.PatientName,
        details.DoctorId,
        details.DoctorName,
        details.StartsAtUtc,
        details.EndsAtUtc,
        details.Status,
        details.ReasonForVisit,
        details.CreatedAtUtc,
        details.UpdatedAtUtc);

    private static DoctorScheduleResponse Map(DoctorSchedule schedule) => new(
        schedule.Id,
        schedule.DoctorId,
        schedule.DayOfWeek,
        schedule.StartsAt,
        schedule.EndsAt,
        schedule.SlotMinutes,
        schedule.IsActive);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    private static Result<T> Failure<T>(string code, string message) => Result<T>.Failure(code, message);
}
