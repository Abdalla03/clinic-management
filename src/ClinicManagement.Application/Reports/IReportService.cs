using ClinicManagement.Application.Common;
using ClinicManagement.Application.Reports;

namespace ClinicManagement.Application.Reports;

public interface IReportService
{
    Task<Result<RevenueReport>> GetRevenueAsync(ReportRangeQuery query, CancellationToken cancellationToken);

    Task<Result<OutstandingReport>> GetOutstandingAsync(ReportRangeQuery query, CancellationToken cancellationToken);

    Task<Result<VisitReport>> GetVisitReportAsync(ReportRangeQuery query, CancellationToken cancellationToken);
}
