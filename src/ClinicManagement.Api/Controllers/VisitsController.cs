using ClinicManagement.Api.Authorization;
using ClinicManagement.Api.Infrastructure;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Visits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagement.Api.Controllers;

[Route("api")]
public sealed class VisitsController(IVisitService visitService) : ClinicControllerBase
{
    [HttpGet("visits")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PagedResult<VisitSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> SearchAsync(
        [FromQuery] Guid? patientId = null,
        [FromQuery] Guid? doctorId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await visitService.SearchVisitsAsync(
            new VisitSearchQuery
            {
                PatientId = patientId,
                DoctorId = doctorId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpGet("visits/mine")]
    [Authorize(Policy = Policies.PatientOnly)]
    [ProducesResponseType(typeof(PagedResult<VisitSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> GetOwnAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await visitService.GetOwnVisitsAsync(
            new PageRequest { Page = page, PageSize = pageSize },
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpGet("visits/{visitId:guid}")]
    [Authorize(Policy = Policies.AuthenticatedUser)]
    [ProducesResponseType(typeof(VisitDetails), StatusCodes.Status200OK)]
    public async Task<IResult> GetAsync(Guid visitId, CancellationToken cancellationToken)
    {
        var result = await visitService.GetVisitAsync(visitId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("visits")]
    [Authorize(Policy = Policies.DoctorOnly)]
    [ProducesResponseType(typeof(VisitDetails), StatusCodes.Status201Created)]
    public async Task<IResult> RecordAsync(
        [FromBody] RecordVisitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await visitService.RecordVisitAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.Created($"/api/visits/{result.Value!.Id}", result.Value)
            : result.ToHttpResult();
    }

    [HttpPut("visits/{visitId:guid}")]
    [Authorize(Policy = Policies.DoctorOnly)]
    [ProducesResponseType(typeof(VisitDetails), StatusCodes.Status200OK)]
    public async Task<IResult> UpdateAsync(
        Guid visitId,
        [FromBody] UpdateVisitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await visitService.UpdateVisitAsync(visitId, request, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("patients/{patientId:guid}/visits")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PagedResult<VisitSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> GetPatientVisitsAsync(
        Guid patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await visitService.GetPatientVisitsAsync(
            patientId,
            new PageRequest { Page = page, PageSize = pageSize },
            cancellationToken);

        return result.ToHttpResult();
    }
}
