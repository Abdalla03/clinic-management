using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Paging;

namespace ClinicManagement.Application.Patients;

public interface IPatientService
{
    Task<Result<PagedResult<PatientSummary>>> SearchPatientsAsync(
        PatientSearchQuery query,
        CancellationToken cancellationToken);

    Task<Result<PatientDetails>> GetPatientAsync(Guid patientId, CancellationToken cancellationToken);

    Task<Result<PatientDetails>> GetOwnRecordAsync(CancellationToken cancellationToken);

    Task<Result<PatientDetails>> CreatePatientAsync(CreatePatientRequest request, CancellationToken cancellationToken);

    Task<Result<PatientDetails>> UpdatePatientAsync(
        Guid patientId,
        UpdatePatientRequest request,
        CancellationToken cancellationToken);

    Task<Result> DeactivatePatientAsync(Guid patientId, CancellationToken cancellationToken);

    Task<Result<PagedResult<MedicalHistoryEntryResponse>>> GetMedicalHistoryAsync(
        Guid patientId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<MedicalHistoryEntryResponse>>> GetOwnMedicalHistoryAsync(
        PageRequest page,
        CancellationToken cancellationToken);

    Task<Result<MedicalHistoryEntryResponse>> AddMedicalHistoryAsync(
        Guid patientId,
        AddMedicalHistoryRequest request,
        CancellationToken cancellationToken);
}
