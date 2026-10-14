using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Paging;

namespace ClinicManagement.Application.Visits;

public interface IVisitService
{
    Task<Result<PagedResult<VisitSummary>>> SearchVisitsAsync(
        VisitSearchQuery query,
        CancellationToken cancellationToken);

    Task<Result<VisitDetails>> GetVisitAsync(Guid visitId, CancellationToken cancellationToken);

    Task<Result<VisitDetails>> RecordVisitAsync(RecordVisitRequest request, CancellationToken cancellationToken);

    Task<Result<VisitDetails>> UpdateVisitAsync(
        Guid visitId,
        UpdateVisitRequest request,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<VisitSummary>>> GetPatientVisitsAsync(
        Guid patientId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<VisitSummary>>> GetOwnVisitsAsync(
        PageRequest page,
        CancellationToken cancellationToken);
}
