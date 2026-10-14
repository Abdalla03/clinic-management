using ClinicManagement.Application.Abstractions;
using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Extensions;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Visits;
using ClinicManagement.Domain.Appointments;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagement.Infrastructure.Visits;

public sealed class VisitService(
    ClinicDbContext dbContext,
    ICurrentUser currentUser,
    IAuditService auditService,
    TimeProvider timeProvider,
    IValidator<RecordVisitRequest> recordValidator,
    IValidator<UpdateVisitRequest> updateValidator,
    IValidator<VisitSearchQuery> searchValidator,
    IValidator<PageRequest> pageValidator) : IVisitService
{
    public async Task<Result<PagedResult<VisitSummary>>> SearchVisitsAsync(
        VisitSearchQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await searchValidator.ValidateAndBindAsync(query, cancellationToken);
        if (validation.Failed)
        {
            return Result<PagedResult<VisitSummary>>.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<PagedResult<VisitSummary>>.Failure(
                ErrorCodes.Forbidden,
                "Patients may only view their own visits.");
        }

        var page = new PageRequest { Page = query.Page, PageSize = query.PageSize };

        var visits = dbContext.PatientVisits.AsNoTracking();

        if (query.PatientId is { } patientId)
        {
            visits = visits.Where(visit => visit.PatientId == patientId);
        }

        if (query.DoctorId is { } doctorId)
        {
            visits = visits.Where(visit => visit.DoctorId == doctorId);
        }

        if (query.FromUtc is { } fromUtc)
        {
            visits = visits.Where(visit => visit.VisitAtUtc >= fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            visits = visits.Where(visit => visit.VisitAtUtc <= toUtc);
        }

        var totalCount = await visits.CountAsync(cancellationToken);

        var items = await visits
            .Include(visit => visit.Patient)
            .OrderByDescending(visit => visit.VisitAtUtc)
            .ThenBy(visit => visit.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        var summaries = await MapSummariesAsync(items, cancellationToken);

        return Result<PagedResult<VisitSummary>>.Success(
            PagedResult<VisitSummary>.Create(summaries, page, totalCount));
    }

    public async Task<Result<VisitDetails>> GetVisitAsync(Guid visitId, CancellationToken cancellationToken)
    {
        var visit = await dbContext.PatientVisits
            .AsNoTracking()
            .Include(candidate => candidate.Patient)
            .FirstOrDefaultAsync(candidate => candidate.Id == visitId, cancellationToken);

        if (visit is null)
        {
            return Result<VisitDetails>.Failure(ErrorCodes.NotFound, "Visit was not found.");
        }

        if (await IsBlockedFromPatientAsync(visit.PatientId, cancellationToken))
        {
            return Result<VisitDetails>.Failure(ErrorCodes.Forbidden, "You may only view your own visits.");
        }

        await auditService.RecordAsync(
            AuditAction.Viewed,
            nameof(PatientVisit),
            visit.Id.ToString(),
            visit.PatientId,
            cancellationToken: cancellationToken);

        return Result<VisitDetails>.Success((await MapDetailsAsync([visit], cancellationToken))[0]);
    }

    public async Task<Result<VisitDetails>> RecordVisitAsync(
        RecordVisitRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await recordValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result<VisitDetails>.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<VisitDetails>.Failure(ErrorCodes.Forbidden, "Patients cannot record visits.");
        }

        if (currentUser.UserId is not { } recordedByUserId)
        {
            return Result<VisitDetails>.Failure(ErrorCodes.Forbidden, "No signed-in user was found.");
        }

        var patient = await dbContext.Patients.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.PatientId, cancellationToken);

        if (patient is null)
        {
            return Result<VisitDetails>.Failure(ErrorCodes.NotFound, "Patient was not found.");
        }

        if (!patient.IsActive)
        {
            return Result<VisitDetails>.Failure(ErrorCodes.Conflict, "The patient record is inactive.");
        }

        var doctor = await dbContext.Doctors.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.DoctorId, cancellationToken);

        if (doctor is null)
        {
            return Result<VisitDetails>.Failure(ErrorCodes.NotFound, "Doctor was not found.");
        }

        var visitAtUtc = NormalizeUtc(request.VisitAtUtc);

        if (request.AppointmentId is { } appointmentId)
        {
            var appointment = await dbContext.Appointments.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == appointmentId, cancellationToken);

            if (appointment is null)
            {
                return Result<VisitDetails>.Failure(ErrorCodes.NotFound, "Appointment was not found.");
            }

            if (appointment.PatientId != request.PatientId || appointment.DoctorId != request.DoctorId)
            {
                return Result<VisitDetails>.Failure(
                    ErrorCodes.ValidationFailed,
                    "The appointment does not belong to that patient and doctor.");
            }

            if (appointment.Status is not (AppointmentStatus.Booked or AppointmentStatus.Confirmed))
            {
                return Result<VisitDetails>.Failure(
                    ErrorCodes.InvalidStateTransition,
                    $"An appointment in the '{appointment.Status}' state cannot be recorded as a visit.");
            }
        }

        var visit = PatientVisit.Record(
            request.PatientId,
            request.DoctorId,
            request.AppointmentId,
            visitAtUtc,
            request.ChiefComplaint,
            request.Notes,
            request.Diagnosis,
            recordedByUserId,
            timeProvider.GetUtcNow().UtcDateTime);

        dbContext.PatientVisits.Add(visit);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Created,
            nameof(PatientVisit),
            visit.Id.ToString(),
            visit.PatientId,
            cancellationToken: cancellationToken);

        var created = await dbContext.PatientVisits
            .Include(candidate => candidate.Patient)
            .SingleAsync(candidate => candidate.Id == visit.Id, cancellationToken);

        return Result<VisitDetails>.Success((await MapDetailsAsync([created], cancellationToken))[0]);
    }

    public async Task<Result<VisitDetails>> UpdateVisitAsync(
        Guid visitId,
        UpdateVisitRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result<VisitDetails>.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<VisitDetails>.Failure(ErrorCodes.Forbidden, "Patients cannot amend clinical notes.");
        }

        var visit = await dbContext.PatientVisits
            .Include(candidate => candidate.Patient)
            .FirstOrDefaultAsync(candidate => candidate.Id == visitId, cancellationToken);

        if (visit is null)
        {
            return Result<VisitDetails>.Failure(ErrorCodes.NotFound, "Visit was not found.");
        }

        var changedFields = new List<string>();

        if (!string.Equals(visit.ChiefComplaint, Normalize(request.ChiefComplaint), StringComparison.Ordinal))
        {
            changedFields.Add(nameof(PatientVisit.ChiefComplaint));
        }

        if (!string.Equals(visit.Notes, Normalize(request.Notes), StringComparison.Ordinal))
        {
            changedFields.Add(nameof(PatientVisit.Notes));
        }

        if (!string.Equals(visit.Diagnosis, Normalize(request.Diagnosis), StringComparison.Ordinal))
        {
            changedFields.Add(nameof(PatientVisit.Diagnosis));
        }

        visit.UpdateClinicalContent(
            request.ChiefComplaint,
            request.Notes,
            request.Diagnosis,
            timeProvider.GetUtcNow().UtcDateTime);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Updated,
            nameof(PatientVisit),
            visit.Id.ToString(),
            visit.PatientId,
            changedFields,
            cancellationToken);

        return Result<VisitDetails>.Success((await MapDetailsAsync([visit], cancellationToken))[0]);
    }

    public async Task<Result<PagedResult<VisitSummary>>> GetPatientVisitsAsync(
        Guid patientId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var pageValidation = await pageValidator.ValidateAndBindAsync(page, cancellationToken);
        if (pageValidation.Failed)
        {
            return Result<PagedResult<VisitSummary>>.Failure(pageValidation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<PagedResult<VisitSummary>>.Failure(
                ErrorCodes.Forbidden,
                "Patients may only view their own visits.");
        }

        var visits = dbContext.PatientVisits.AsNoTracking().Where(visit => visit.PatientId == patientId);

        var totalCount = await visits.CountAsync(cancellationToken);

        var items = await visits
            .Include(visit => visit.Patient)
            .OrderByDescending(visit => visit.VisitAtUtc)
            .ThenBy(visit => visit.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        var summaries = await MapSummariesAsync(items, cancellationToken);

        return Result<PagedResult<VisitSummary>>.Success(
            PagedResult<VisitSummary>.Create(summaries, page, totalCount));
    }

    public async Task<Result<PagedResult<VisitSummary>>> GetOwnVisitsAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var pageValidation = await pageValidator.ValidateAndBindAsync(page, cancellationToken);
        if (pageValidation.Failed)
        {
            return Result<PagedResult<VisitSummary>>.Failure(pageValidation.Error!);
        }

        var patientId = await ResolveOwnPatientIdAsync(cancellationToken);
        if (patientId == Guid.Empty)
        {
            return Result<PagedResult<VisitSummary>>.Failure(
                ErrorCodes.NotFound,
                "No patient record is linked to this account.");
        }

        var visits = dbContext.PatientVisits.AsNoTracking().Where(visit => visit.PatientId == patientId);

        var totalCount = await visits.CountAsync(cancellationToken);

        var items = await visits
            .Include(visit => visit.Patient)
            .OrderByDescending(visit => visit.VisitAtUtc)
            .ThenBy(visit => visit.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        var summaries = await MapSummariesAsync(items, cancellationToken);

        return Result<PagedResult<VisitSummary>>.Success(
            PagedResult<VisitSummary>.Create(summaries, page, totalCount));
    }

    private async Task<IReadOnlyList<VisitSummary>> MapSummariesAsync(
        IReadOnlyCollection<PatientVisit> visits,
        CancellationToken cancellationToken)
    {
        if (visits.Count == 0)
        {
            return [];
        }

        var doctorNames = await ResolveDoctorNamesAsync(
            visits.Select(visit => visit.DoctorId).Distinct().ToArray(),
            cancellationToken);

        return visits
            .Select(visit => new VisitSummary(
                visit.Id,
                visit.PatientId,
                visit.Patient?.FullName ?? string.Empty,
                visit.DoctorId,
                doctorNames.GetValueOrDefault(visit.DoctorId, string.Empty),
                visit.AppointmentId,
                visit.VisitAtUtc,
                visit.Diagnosis,
                visit.CreatedAtUtc))
            .ToArray();
    }

    private async Task<IReadOnlyList<VisitDetails>> MapDetailsAsync(
        IReadOnlyCollection<PatientVisit> visits,
        CancellationToken cancellationToken)
    {
        if (visits.Count == 0)
        {
            return [];
        }

        var doctorNames = await ResolveDoctorNamesAsync(
            visits.Select(visit => visit.DoctorId).Distinct().ToArray(),
            cancellationToken);

        return visits
            .Select(visit => new VisitDetails(
                visit.Id,
                visit.PatientId,
                visit.Patient?.FullName ?? string.Empty,
                visit.DoctorId,
                doctorNames.GetValueOrDefault(visit.DoctorId, string.Empty),
                visit.AppointmentId,
                visit.VisitAtUtc,
                visit.ChiefComplaint,
                visit.Notes,
                visit.Diagnosis,
                visit.RecordedByUserId,
                visit.CreatedAtUtc))
            .ToArray();
    }

    private async Task<Dictionary<Guid, string>> ResolveDoctorNamesAsync(
        IReadOnlyCollection<Guid> doctorIds,
        CancellationToken cancellationToken) =>
        doctorIds.Count == 0
            ? []
            : await dbContext.Doctors
                .AsNoTracking()
                .Where(doctor => doctorIds.Contains(doctor.Id))
                .Join(
                    dbContext.Users,
                    doctor => doctor.ApplicationUserId,
                    user => user.Id,
                    (doctor, user) => new { doctor.Id, user.FullName })
                .ToDictionaryAsync(pair => pair.Id, pair => pair.FullName, cancellationToken);

    private async Task<bool> IsBlockedFromPatientAsync(Guid patientId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(RoleNames.Patient))
        {
            return false;
        }

        var ownPatientId = await ResolveOwnPatientIdAsync(cancellationToken);
        return ownPatientId == Guid.Empty || ownPatientId != patientId;
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

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
