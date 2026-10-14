using ClinicManagement.Api.Authorization;
using ClinicManagement.Api.Infrastructure;
using ClinicManagement.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagement.Api.Controllers;

[Route("api/reports")]
public sealed class ReportsController(IReportService reportService) : ClinicControllerBase
{
    [HttpGet("revenue")]
    [Authorize(Policy = Policies.SecretaryOnly)]
    [ProducesResponseType(typeof(RevenueReport), StatusCodes.Status200OK)]
    public async Task<IResult> GetRevenueAsync(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await reportService.GetRevenueAsync(
            new ReportRangeQuery(fromUtc, toUtc),
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpGet("outstanding")]
    [Authorize(Policy = Policies.SecretaryOnly)]
    [ProducesResponseType(typeof(OutstandingReport), StatusCodes.Status200OK)]
    public async Task<IResult> GetOutstandingAsync(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await reportService.GetOutstandingAsync(
            new ReportRangeQuery(fromUtc, toUtc),
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpGet("visits")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(VisitReport), StatusCodes.Status200OK)]
    public async Task<IResult> GetVisitReportAsync(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await reportService.GetVisitReportAsync(
            new ReportRangeQuery(fromUtc, toUtc),
            cancellationToken);

        return result.ToHttpResult();
    }
}
