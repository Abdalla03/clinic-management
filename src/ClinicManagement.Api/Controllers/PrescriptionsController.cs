using ClinicManagement.Api.Authorization;
using ClinicManagement.Api.Infrastructure;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Prescriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagement.Api.Controllers;

[Route("api")]
public sealed class PrescriptionsController(IPrescriptionService prescriptionService) : ClinicControllerBase
{
    [HttpGet("prescriptions")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PagedResult<PrescriptionSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> SearchAsync(
        [FromQuery] Guid? patientId = null,
        [FromQuery] Guid? doctorId = null,
        [FromQuery] Guid? visitId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await prescriptionService.SearchPrescriptionsAsync(
            new PrescriptionSearchQuery
            {
                PatientId = patientId,
                DoctorId = doctorId,
                VisitId = visitId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpGet("prescriptions/mine")]
    [Authorize(Policy = Policies.PatientOnly)]
    [ProducesResponseType(typeof(PagedResult<PrescriptionSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> GetOwnAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await prescriptionService.GetOwnPrescriptionsAsync(
            new PageRequest { Page = page, PageSize = pageSize },
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpGet("prescriptions/{prescriptionId:guid}")]
    [Authorize(Policy = Policies.AuthenticatedUser)]
    [ProducesResponseType(typeof(PrescriptionDetails), StatusCodes.Status200OK)]
    public async Task<IResult> GetAsync(Guid prescriptionId, CancellationToken cancellationToken)
    {
        var result = await prescriptionService.GetPrescriptionAsync(prescriptionId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("prescriptions")]
    [Authorize(Policy = Policies.DoctorOnly)]
    [ProducesResponseType(typeof(PrescriptionDetails), StatusCodes.Status201Created)]
    public async Task<IResult> IssueAsync(
        [FromBody] IssuePrescriptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await prescriptionService.IssuePrescriptionAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.Created($"/api/prescriptions/{result.Value!.Id}", result.Value)
            : result.ToHttpResult();
    }

    [HttpGet("patients/{patientId:guid}/prescriptions")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PagedResult<PrescriptionSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> GetPatientPrescriptionsAsync(
        Guid patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await prescriptionService.GetPatientPrescriptionsAsync(
            patientId,
            new PageRequest { Page = page, PageSize = pageSize },
            cancellationToken);

        return result.ToHttpResult();
    }
}
