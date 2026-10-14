using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Domain.Patients;

namespace ClinicManagement.Application.Patients;

public sealed record PatientSummary(
    Guid Id,
    string MedicalRecordNumber,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    Gender? Gender,
    BloodType? BloodType,
    bool IsActive,
    DateTime UpdatedAtUtc);

public sealed record PatientDetails(
    Guid Id,
    string MedicalRecordNumber,
    string FirstName,
    string LastName,
    string FullName,
    string? NationalId,
    string? PhoneNumber,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    DateOnly? DateOfBirth,
    Gender? Gender,
    BloodType? BloodType,
    string? Address,
    string? Notes,
    bool IsActive,
    Guid? ApplicationUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid Version);

public sealed record CreatePatientRequest(
    string MedicalRecordNumber,
    string FirstName,
    string LastName,
    string? NationalId = null,
    string? PhoneNumber = null,
    DateOnly? DateOfBirth = null,
    Gender? Gender = null,
    BloodType? BloodType = null,
    string? Address = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? Notes = null);

public sealed record UpdatePatientRequest(
    string FirstName,
    string LastName,
    string? NationalId = null,
    string? PhoneNumber = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    DateOnly? DateOfBirth = null,
    Gender? Gender = null,
    BloodType? BloodType = null,
    string? Address = null,
    string? Notes = null);

public sealed record PatientSearchQuery
{
    public string? Query { get; init; }

    public bool? IsActive { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
}

public sealed record AddMedicalHistoryRequest(
    string ConditionName,
    DateOnly? DiagnosedOn = null,
    string? Notes = null);

public sealed record MedicalHistoryEntryResponse(
    Guid Id,
    Guid PatientId,
    string ConditionName,
    DateOnly? DiagnosedOn,
    bool IsActive,
    string? Notes,
    Guid RecordedByUserId,
    DateTime RecordedAtUtc);
