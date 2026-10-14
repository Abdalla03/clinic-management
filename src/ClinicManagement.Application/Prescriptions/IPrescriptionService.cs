using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Paging;

namespace ClinicManagement.Application.Prescriptions;

public interface IPrescriptionService
{
    Task<Result<PagedResult<PrescriptionSummary>>> SearchPrescriptionsAsync(
        PrescriptionSearchQuery query,
        CancellationToken cancellationToken);

    Task<Result<PrescriptionDetails>> GetPrescriptionAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken);

    Task<Result<PrescriptionDetails>> IssuePrescriptionAsync(
        IssuePrescriptionRequest request,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<PrescriptionSummary>>> GetPatientPrescriptionsAsync(
        Guid patientId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<PrescriptionSummary>>> GetOwnPrescriptionsAsync(
        PageRequest page,
        CancellationToken cancellationToken);
}
