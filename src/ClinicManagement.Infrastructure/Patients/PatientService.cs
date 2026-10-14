using ClinicManagement.Application.Abstractions;
using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Extensions;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Patients;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClinicManagement.Infrastructure.Patients;

public sealed class PatientService(
    ClinicDbContext dbContext,
    ICurrentUser currentUser,
    IAuditService auditService,
    TimeProvider timeProvider,
    IValidator<PatientSearchQuery> searchValidator,
    IValidator<CreatePatientRequest> createValidator,
    IValidator<UpdatePatientRequest> updateValidator,
    IValidator<AddMedicalHistoryRequest> historyValidator,
    IValidator<PageRequest> pageValidator) : IPatientService
{
    public async Task<Result<PagedResult<PatientSummary>>> SearchPatientsAsync(
        PatientSearchQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await searchValidator.ValidateAndBindAsync(query, cancellationToken);
        if (validation.Failed)
        {
            return Result<PagedResult<PatientSummary>>.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<PagedResult<PatientSummary>>.Failure(
                ErrorCodes.Forbidden,
                "Patients may only view their own record.");
        }

        var page = new PageRequest { Page = query.Page, PageSize = query.PageSize };

        var patients = dbContext.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var term = query.Query.Trim().ToLower();
            patients = patients.Where(patient =>
                patient.MedicalRecordNumber.ToLower().Contains(term) ||
                patient.FirstName.ToLower().Contains(term) ||
                patient.LastName.ToLower().Contains(term) ||
                (patient.FirstName + " " + patient.LastName).ToLower().Contains(term));
        }

        if (query.IsActive is { } isActive)
        {
            patients = patients.Where(patient => patient.IsActive == isActive);
        }

        var totalCount = await patients.CountAsync(cancellationToken);

        var items = await patients
            .OrderBy(patient => patient.LastName)
            .ThenBy(patient => patient.FirstName)
            .ThenBy(patient => patient.MedicalRecordNumber)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(patient => new PatientSummary(
                patient.Id,
                patient.MedicalRecordNumber,
                patient.FirstName,
                patient.LastName,
                patient.PhoneNumber,
                patient.DateOfBirth,
                patient.Gender,
                patient.BloodType,
                patient.IsActive,
                patient.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<PatientSummary>>.Success(PagedResult<PatientSummary>.Create(items, page, totalCount));
    }

    public async Task<Result<PatientDetails>> GetPatientAsync(Guid patientId, CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<PatientDetails>(ErrorCodes.Forbidden, "Patients may only view their own record.");
        }

        var patient = await dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == patientId, cancellationToken);

        if (patient is null)
        {
            return Failure<PatientDetails>(ErrorCodes.NotFound, "Patient was not found.");
        }

        await auditService.RecordAsync(
            AuditAction.Viewed,
            nameof(Patient),
            patient.Id.ToString(),
            patient.Id,
            cancellationToken: cancellationToken);

        return Result<PatientDetails>.Success(Map(patient));
    }

    public async Task<Result<PatientDetails>> GetOwnRecordAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Failure<PatientDetails>(ErrorCodes.Forbidden, "No signed-in user was found.");
        }

        var patient = await dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ApplicationUserId == userId, cancellationToken);

        if (patient is null)
        {
            return Failure<PatientDetails>(ErrorCodes.NotFound, "No patient record is linked to this account.");
        }

        await auditService.RecordAsync(
            AuditAction.Viewed,
            nameof(Patient),
            patient.Id.ToString(),
            patient.Id,
            cancellationToken: cancellationToken);

        return Result<PatientDetails>.Success(Map(patient));
    }

    public async Task<Result<PatientDetails>> CreatePatientAsync(
        CreatePatientRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Failure<PatientDetails>(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<PatientDetails>(ErrorCodes.Forbidden, "Patients cannot create records.");
        }

        var medicalRecordNumber = request.MedicalRecordNumber.Trim();
        if (await dbContext.Patients.AnyAsync(
                patient => patient.MedicalRecordNumber == medicalRecordNumber,
                cancellationToken))
        {
            return Failure<PatientDetails>(
                ErrorCodes.Conflict,
                $"A patient with medical record number '{medicalRecordNumber}' already exists.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var patient = Patient.Create(medicalRecordNumber, request.FirstName, request.LastName, now);
        patient.UpdateContactDetails(
            request.NationalId,
            request.PhoneNumber,
            request.EmergencyContactName,
            request.EmergencyContactPhone,
            now);
        patient.UpdateDemographics(request.DateOfBirth, request.Gender, request.BloodType, now);
        patient.UpdateNotes(request.Notes, now);

        dbContext.Patients.Add(patient);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Failure<PatientDetails>(
                ErrorCodes.Conflict,
                $"A patient with medical record number '{medicalRecordNumber}' already exists.");
        }

        await auditService.RecordAsync(
            AuditAction.Created,
            nameof(Patient),
            patient.Id.ToString(),
            patient.Id,
            cancellationToken: cancellationToken);

        return Result<PatientDetails>.Success(Map(patient));
    }

    public async Task<Result<PatientDetails>> UpdatePatientAsync(
        Guid patientId,
        UpdatePatientRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Failure<PatientDetails>(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<PatientDetails>(ErrorCodes.Forbidden, "Patients cannot edit records.");
        }

        var patient = await dbContext.Patients
            .FirstOrDefaultAsync(candidate => candidate.Id == patientId, cancellationToken);

        if (patient is null)
        {
            return Failure<PatientDetails>(ErrorCodes.NotFound, "Patient was not found.");
        }

        var before = Snapshot(patient);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        patient.Rename(request.FirstName, request.LastName, now);
        patient.UpdateContactDetails(
            request.NationalId,
            request.PhoneNumber,
            request.EmergencyContactName,
            request.EmergencyContactPhone,
            now);
        patient.UpdateDemographics(request.DateOfBirth, request.Gender, request.BloodType, now);
        patient.UpdateNotes(request.Notes, now);

        var changedFields = Diff(before, Snapshot(patient));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure<PatientDetails>(
                ErrorCodes.ConcurrencyConflict,
                "The patient record was modified by someone else. Reload it and try again.");
        }

        await auditService.RecordAsync(
            AuditAction.Updated,
            nameof(Patient),
            patient.Id.ToString(),
            patient.Id,
            changedFields,
            cancellationToken);

        return Result<PatientDetails>.Success(Map(patient));
    }

    public async Task<Result> DeactivatePatientAsync(Guid patientId, CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result.Failure(ErrorCodes.Forbidden, "Patients cannot deactivate records.");
        }

        var patient = await dbContext.Patients
            .FirstOrDefaultAsync(candidate => candidate.Id == patientId, cancellationToken);

        if (patient is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Patient was not found.");
        }

        if (!patient.IsActive)
        {
            return Result.Failure(ErrorCodes.Conflict, "Patient is already deactivated.");
        }

        patient.Deactivate(timeProvider.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Deleted,
            nameof(Patient),
            patient.Id.ToString(),
            patient.Id,
            ["IsActive"],
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<PagedResult<MedicalHistoryEntryResponse>>> GetMedicalHistoryAsync(
        Guid patientId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<PagedResult<MedicalHistoryEntryResponse>>(
                ErrorCodes.Forbidden,
                "Patients may only view their own record.");
        }

        var pageValidation = await pageValidator.ValidateAndBindAsync(page, cancellationToken);
        if (pageValidation.Failed)
        {
            return Failure<PagedResult<MedicalHistoryEntryResponse>>(pageValidation.Error!);
        }

        if (!await dbContext.Patients.AnyAsync(patient => patient.Id == patientId, cancellationToken))
        {
            return Failure<PagedResult<MedicalHistoryEntryResponse>>(ErrorCodes.NotFound, "Patient was not found.");
        }

        return await QueryHistoryAsync(patientId, page, cancellationToken);
    }

    public async Task<Result<PagedResult<MedicalHistoryEntryResponse>>> GetOwnMedicalHistoryAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var lookup = await ResolveOwnPatientIdAsync(cancellationToken);
        if (lookup.Failed)
        {
            return Failure<PagedResult<MedicalHistoryEntryResponse>>(lookup.Error!);
        }

        var pageValidation = await pageValidator.ValidateAndBindAsync(page, cancellationToken);
        if (pageValidation.Failed)
        {
            return Failure<PagedResult<MedicalHistoryEntryResponse>>(pageValidation.Error!);
        }

        return await QueryHistoryAsync(lookup.Value, page, cancellationToken);
    }

    public async Task<Result<MedicalHistoryEntryResponse>> AddMedicalHistoryAsync(
        Guid patientId,
        AddMedicalHistoryRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await historyValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Failure<MedicalHistoryEntryResponse>(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Failure<MedicalHistoryEntryResponse>(ErrorCodes.Forbidden, "Patients cannot add medical history.");
        }

        if (!await dbContext.Patients.AnyAsync(patient => patient.Id == patientId, cancellationToken))
        {
            return Failure<MedicalHistoryEntryResponse>(ErrorCodes.NotFound, "Patient was not found.");
        }

        if (currentUser.UserId is not { } recordedByUserId)
        {
            return Failure<MedicalHistoryEntryResponse>(ErrorCodes.Forbidden, "No signed-in user was found.");
        }

        var entry = MedicalHistoryEntry.Record(
            patientId,
            request.ConditionName,
            request.DiagnosedOn,
            request.Notes,
            recordedByUserId,
            timeProvider.GetUtcNow().UtcDateTime);

        dbContext.MedicalHistoryEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Created,
            nameof(MedicalHistoryEntry),
            entry.Id.ToString(),
            patientId,
            [nameof(MedicalHistoryEntry.ConditionName), nameof(MedicalHistoryEntry.DiagnosedOn)],
            cancellationToken);

        return Result<MedicalHistoryEntryResponse>.Success(Map(entry));
    }

    private async Task<Result<Guid>> ResolveOwnPatientIdAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<Guid>.Failure(ErrorCodes.Forbidden, "No signed-in user was found.");
        }

        var patientId = await dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.ApplicationUserId == userId)
            .Select(patient => patient.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (patientId == Guid.Empty)
        {
            return Result<Guid>.Failure(ErrorCodes.NotFound, "No patient record is linked to this account.");
        }

        return Result<Guid>.Success(patientId);
    }

    private async Task<Result<PagedResult<MedicalHistoryEntryResponse>>> QueryHistoryAsync(
        Guid patientId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var entries = dbContext.MedicalHistoryEntries
            .AsNoTracking()
            .Where(entry => entry.PatientId == patientId);

        var totalCount = await entries.CountAsync(cancellationToken);

        var items = await entries
            .OrderByDescending(entry => entry.RecordedAtUtc)
            .ThenBy(entry => entry.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(entry => new MedicalHistoryEntryResponse(
                entry.Id,
                entry.PatientId,
                entry.ConditionName,
                entry.DiagnosedOn,
                entry.IsActive,
                entry.Notes,
                entry.RecordedByUserId,
                entry.RecordedAtUtc))
            .ToListAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Viewed,
            nameof(MedicalHistoryEntry),
            patientId: patientId,
            cancellationToken: cancellationToken);

        return Result<PagedResult<MedicalHistoryEntryResponse>>.Success(
            PagedResult<MedicalHistoryEntryResponse>.Create(items, page, totalCount));
    }

    private static Dictionary<string, string?> Snapshot(Patient patient) => new(StringComparer.Ordinal)
    {
        ["firstName"] = patient.FirstName,
        ["lastName"] = patient.LastName,
        ["nationalId"] = patient.NationalId,
        ["phoneNumber"] = patient.PhoneNumber,
        ["emergencyContactName"] = patient.EmergencyContactName,
        ["emergencyContactPhone"] = patient.EmergencyContactPhone,
        ["dateOfBirth"] = patient.DateOfBirth?.ToString("O"),
        ["gender"] = patient.Gender?.ToString(),
        ["bloodType"] = patient.BloodType?.ToString(),
        ["address"] = patient.Address,
        ["notes"] = patient.Notes
    };

    private static IReadOnlyCollection<string> Diff(
        Dictionary<string, string?> before,
        Dictionary<string, string?> after) =>
        after
            .Where(pair => !string.Equals(before[pair.Key], pair.Value, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static PatientDetails Map(Patient patient) => new(
        patient.Id,
        patient.MedicalRecordNumber,
        patient.FirstName,
        patient.LastName,
        patient.FullName,
        patient.NationalId,
        patient.PhoneNumber,
        patient.EmergencyContactName,
        patient.EmergencyContactPhone,
        patient.DateOfBirth,
        patient.Gender,
        patient.BloodType,
        patient.Address,
        patient.Notes,
        patient.IsActive,
        patient.ApplicationUserId,
        patient.CreatedAtUtc,
        patient.UpdatedAtUtc,
        patient.Version);

    private static MedicalHistoryEntryResponse Map(MedicalHistoryEntry entry) => new(
        entry.Id,
        entry.PatientId,
        entry.ConditionName,
        entry.DiagnosedOn,
        entry.IsActive,
        entry.Notes,
        entry.RecordedByUserId,
        entry.RecordedAtUtc);

    private static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    private static Result<T> Failure<T>(string code, string message) => Result<T>.Failure(code, message);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
