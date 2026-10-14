using ClinicManagement.Application.Abstractions;
using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Extensions;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Prescriptions;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Prescriptions;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagement.Infrastructure.Prescriptions;

public sealed class PrescriptionService(
    ClinicDbContext dbContext,
    ICurrentUser currentUser,
    IAuditService auditService,
    TimeProvider timeProvider,
    IValidator<IssuePrescriptionRequest> issueValidator,
    IValidator<PrescriptionSearchQuery> searchValidator,
    IValidator<PageRequest> pageValidator) : IPrescriptionService
{
    public async Task<Result<PagedResult<PrescriptionSummary>>> SearchPrescriptionsAsync(
        PrescriptionSearchQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await searchValidator.ValidateAndBindAsync(query, cancellationToken);
        if (validation.Failed)
        {
            return Result<PagedResult<PrescriptionSummary>>.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<PagedResult<PrescriptionSummary>>.Failure(
                ErrorCodes.Forbidden,
                "Patients may only view their own prescriptions.");
        }

        var page = new PageRequest { Page = query.Page, PageSize = query.PageSize };

        var prescriptions = dbContext.Prescriptions.AsNoTracking();

        if (query.PatientId is { } patientId)
        {
            prescriptions = prescriptions.Where(prescription => prescription.PatientId == patientId);
        }

        if (query.DoctorId is { } doctorId)
        {
            prescriptions = prescriptions.Where(prescription => prescription.DoctorId == doctorId);
        }

        if (query.VisitId is { } visitId)
        {
            prescriptions = prescriptions.Where(prescription => prescription.VisitId == visitId);
        }

        if (query.FromUtc is { } fromUtc)
        {
            prescriptions = prescriptions.Where(prescription => prescription.IssuedAtUtc >= fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            prescriptions = prescriptions.Where(prescription => prescription.IssuedAtUtc <= toUtc);
        }

        var totalCount = await prescriptions.CountAsync(cancellationToken);

        var items = await prescriptions
            .Include(prescription => prescription.Patient)
            .Include(prescription => prescription.Items)
            .OrderByDescending(prescription => prescription.IssuedAtUtc)
            .ThenBy(prescription => prescription.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<PrescriptionSummary>>.Success(
            PagedResult<PrescriptionSummary>.Create(
                await MapSummariesAsync(items, cancellationToken),
                page,
                totalCount));
    }

    public async Task<Result<PrescriptionDetails>> GetPrescriptionAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken)
    {
        var prescription = await dbContext.Prescriptions
            .AsNoTracking()
            .Include(candidate => candidate.Patient)
            .Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(candidate => candidate.Id == prescriptionId, cancellationToken);

        if (prescription is null)
        {
            return Result<PrescriptionDetails>.Failure(ErrorCodes.NotFound, "Prescription was not found.");
        }

        if (await IsBlockedFromPatientAsync(prescription.PatientId, cancellationToken))
        {
            return Result<PrescriptionDetails>.Failure(
                ErrorCodes.Forbidden,
                "You may only view your own prescriptions.");
        }

        await auditService.RecordAsync(
            AuditAction.Viewed,
            nameof(Prescription),
            prescription.Id.ToString(),
            prescription.PatientId,
            cancellationToken: cancellationToken);

        return Result<PrescriptionDetails>.Success((await MapDetailsAsync([prescription], cancellationToken))[0]);
    }

    public async Task<Result<PrescriptionDetails>> IssuePrescriptionAsync(
        IssuePrescriptionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await issueValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result<PrescriptionDetails>.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<PrescriptionDetails>.Failure(ErrorCodes.Forbidden, "Patients cannot issue prescriptions.");
        }

        if (currentUser.UserId is not { } issuedByUserId)
        {
            return Result<PrescriptionDetails>.Failure(ErrorCodes.Forbidden, "No signed-in user was found.");
        }

        var patient = await dbContext.Patients.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.PatientId, cancellationToken);

        if (patient is null)
        {
            return Result<PrescriptionDetails>.Failure(ErrorCodes.NotFound, "Patient was not found.");
        }

        if (!patient.IsActive)
        {
            return Result<PrescriptionDetails>.Failure(ErrorCodes.Conflict, "The patient record is inactive.");
        }

        if (request.VisitId is { } visitId)
        {
            var visitBelongsToPatient = await dbContext.PatientVisits.AsNoTracking()
                .AnyAsync(candidate => candidate.Id == visitId && candidate.PatientId == request.PatientId,
                    cancellationToken);

            if (!visitBelongsToPatient)
            {
                return Result<PrescriptionDetails>.Failure(
                    ErrorCodes.ValidationFailed,
                    "The visit does not belong to that patient.");
            }
        }

        var allergies = (await dbContext.Allergies
            .AsNoTracking()
            .Where(allergy => allergy.PatientId == request.PatientId)
            .Select(allergy => new { allergy.Id, allergy.NormalizedSubstance })
            .ToListAsync(cancellationToken))
            .Select(allergy => (allergy.Id, allergy.NormalizedSubstance))
            .ToArray();

        var prescription = Prescription.Create(
            request.PatientId,
            ResolveDoctorIdForUser(issuedByUserId),
            request.VisitId,
            request.Notes,
            timeProvider.GetUtcNow().UtcDateTime);

        dbContext.Prescriptions.Add(prescription);

        try
        {
            foreach (var item in request.Items)
            {
                var (outcome, matchedAllergyId) = CheckAllergy(allergies, item.DrugName);

                prescription.AddItem(
                    item.DrugName,
                    item.Dose,
                    item.Route,
                    item.TimesPerDay,
                    item.DurationDays,
                    item.Instructions,
                    item.DoseTimes?.Select(doseTime => new DoseTime(doseTime)),
                    outcome,
                    matchedAllergyId,
                    timeProvider.GetUtcNow().UtcDateTime);
            }
        }
        catch (DomainException exception)
        {
            dbContext.Prescriptions.Remove(prescription);
            return Result<PrescriptionDetails>.Failure(exception.Code, exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Created,
            nameof(Prescription),
            prescription.Id.ToString(),
            prescription.PatientId,
            cancellationToken: cancellationToken);

        var created = await dbContext.Prescriptions
            .Include(candidate => candidate.Patient)
            .Include(candidate => candidate.Items)
            .SingleAsync(candidate => candidate.Id == prescription.Id, cancellationToken);

        return Result<PrescriptionDetails>.Success((await MapDetailsAsync([created], cancellationToken))[0]);
    }

    public async Task<Result<PagedResult<PrescriptionSummary>>> GetPatientPrescriptionsAsync(
        Guid patientId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var pageValidation = await pageValidator.ValidateAndBindAsync(page, cancellationToken);
        if (pageValidation.Failed)
        {
            return Result<PagedResult<PrescriptionSummary>>.Failure(pageValidation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<PagedResult<PrescriptionSummary>>.Failure(
                ErrorCodes.Forbidden,
                "Patients may only view their own prescriptions.");
        }

        var prescriptions = dbContext.Prescriptions
            .AsNoTracking()
            .Where(prescription => prescription.PatientId == patientId);

        var totalCount = await prescriptions.CountAsync(cancellationToken);

        var items = await prescriptions
            .Include(prescription => prescription.Patient)
            .Include(prescription => prescription.Items)
            .OrderByDescending(prescription => prescription.IssuedAtUtc)
            .ThenBy(prescription => prescription.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<PrescriptionSummary>>.Success(
            PagedResult<PrescriptionSummary>.Create(
                await MapSummariesAsync(items, cancellationToken),
                page,
                totalCount));
    }

    public async Task<Result<PagedResult<PrescriptionSummary>>> GetOwnPrescriptionsAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var pageValidation = await pageValidator.ValidateAndBindAsync(page, cancellationToken);
        if (pageValidation.Failed)
        {
            return Result<PagedResult<PrescriptionSummary>>.Failure(pageValidation.Error!);
        }

        var patientId = await ResolveOwnPatientIdAsync(cancellationToken);
        if (patientId == Guid.Empty)
        {
            return Result<PagedResult<PrescriptionSummary>>.Failure(
                ErrorCodes.NotFound,
                "No patient record is linked to this account.");
        }

        var prescriptions = dbContext.Prescriptions
            .AsNoTracking()
            .Where(prescription => prescription.PatientId == patientId);

        var totalCount = await prescriptions.CountAsync(cancellationToken);

        var items = await prescriptions
            .Include(prescription => prescription.Patient)
            .Include(prescription => prescription.Items)
            .OrderByDescending(prescription => prescription.IssuedAtUtc)
            .ThenBy(prescription => prescription.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<PrescriptionSummary>>.Success(
            PagedResult<PrescriptionSummary>.Create(
                await MapSummariesAsync(items, cancellationToken),
                page,
                totalCount));
    }

    private Guid ResolveDoctorIdForUser(Guid applicationUserId) =>
        dbContext.Doctors
            .Where(doctor => doctor.ApplicationUserId == applicationUserId)
            .Select(doctor => doctor.Id)
            .FirstOrDefault();

    private static (AllergyCheckOutcome Outcome, Guid? MatchedAllergyId) CheckAllergy(
        IReadOnlyCollection<(Guid Id, string NormalizedSubstance)> allergy,
        string drugName)
    {
        var normalizedDrugName = SubstanceNormalizer.Normalize(drugName);

        var match = allergy.FirstOrDefault(entry =>
            string.Equals(entry.NormalizedSubstance, normalizedDrugName, StringComparison.OrdinalIgnoreCase));

        return match.Id == Guid.Empty
            ? (AllergyCheckOutcome.NoKnownAllergy, null)
            : (AllergyCheckOutcome.MatchedAllergy, match.Id);
    }

    private async Task<IReadOnlyList<PrescriptionSummary>> MapSummariesAsync(
        IReadOnlyCollection<Prescription> prescriptions,
        CancellationToken cancellationToken)
    {
        if (prescriptions.Count == 0)
        {
            return [];
        }

        var doctorNames = await ResolveDoctorNamesAsync(
            prescriptions.Select(prescription => prescription.DoctorId).Distinct().ToArray(),
            cancellationToken);

        return prescriptions
            .Select(prescription => new PrescriptionSummary(
                prescription.Id,
                prescription.PatientId,
                prescription.Patient?.FullName ?? string.Empty,
                prescription.DoctorId,
                doctorNames.GetValueOrDefault(prescription.DoctorId, string.Empty),
                prescription.VisitId,
                prescription.IssuedAtUtc,
                prescription.Notes,
                prescription.Items.Count,
                prescription.Items.Any(item => item.AllergyCheckOutcome == AllergyCheckOutcome.MatchedAllergy)))
            .ToArray();
    }

    private async Task<IReadOnlyList<PrescriptionDetails>> MapDetailsAsync(
        IReadOnlyCollection<Prescription> prescriptions,
        CancellationToken cancellationToken)
    {
        if (prescriptions.Count == 0)
        {
            return [];
        }

        var doctorNames = await ResolveDoctorNamesAsync(
            prescriptions.Select(prescription => prescription.DoctorId).Distinct().ToArray(),
            cancellationToken);

        return prescriptions
            .Select(prescription => new PrescriptionDetails(
                prescription.Id,
                prescription.PatientId,
                prescription.Patient?.FullName ?? string.Empty,
                prescription.DoctorId,
                doctorNames.GetValueOrDefault(prescription.DoctorId, string.Empty),
                prescription.VisitId,
                prescription.IssuedAtUtc,
                prescription.Notes,
                prescription.Items
                    .OrderBy(item => item.DrugName, StringComparer.Ordinal)
                    .Select(item => new PrescriptionItemResponse(
                        item.Id,
                        item.DrugName,
                        item.Dose,
                        item.Route,
                        item.TimesPerDay,
                        item.DurationDays,
                        item.Instructions,
                        item.DoseTimes.Select(doseTime => doseTime.Utc).ToArray(),
                        item.AllergyCheckOutcome,
                        item.MatchedAllergyId))
                    .ToArray()))
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
}
