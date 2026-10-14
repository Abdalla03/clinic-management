using ClinicManagement.Api.Authorization;
using ClinicManagement.Api.Infrastructure;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Patients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagement.Api.Controllers;

[Route("api/patients")]
public sealed class PatientsController(IPatientService patientService) : ClinicControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PagedResult<PatientSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> SearchAsync(
        [FromQuery] string? query = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var search = new PatientSearchQuery
        {
            Query = query,
            IsActive = isActive,
            Page = page,
            PageSize = pageSize
        };

        var result = await patientService.SearchPatientsAsync(search, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("{patientId:guid}")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PatientDetails), StatusCodes.Status200OK)]
    public async Task<IResult> GetAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var result = await patientService.GetPatientAsync(patientId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PatientDetails), StatusCodes.Status201Created)]
    public async Task<IResult> CreateAsync(
        [FromBody] CreatePatientRequest request,
        CancellationToken cancellationToken)
    {
        var result = await patientService.CreatePatientAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.Created($"/api/patients/{result.Value!.Id}", result.Value)
            : result.ToHttpResult();
    }

    [HttpPut("{patientId:guid}")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PatientDetails), StatusCodes.Status200OK)]
    public async Task<IResult> UpdateAsync(
        Guid patientId,
        [FromBody] UpdatePatientRequest request,
        CancellationToken cancellationToken)
    {
        var result = await patientService.UpdatePatientAsync(patientId, request, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpDelete("{patientId:guid}")]
    [Authorize(Policy = Policies.SecretaryOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> DeactivateAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var result = await patientService.DeactivatePatientAsync(patientId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("{patientId:guid}/medical-history")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PagedResult<MedicalHistoryEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IResult> GetMedicalHistoryAsync(
        Guid patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await patientService.GetMedicalHistoryAsync(
            patientId,
            new PageRequest { Page = page, PageSize = pageSize },
            cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("{patientId:guid}/medical-history")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(MedicalHistoryEntryResponse), StatusCodes.Status201Created)]
    public async Task<IResult> AddMedicalHistoryAsync(
        Guid patientId,
        [FromBody] AddMedicalHistoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await patientService.AddMedicalHistoryAsync(patientId, request, cancellationToken);

        return result.Succeeded
            ? Results.Created($"/api/patients/{patientId}/medical-history/{result.Value!.Id}", result.Value)
            : result.ToHttpResult();
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.PatientOnly)]
    [ProducesResponseType(typeof(PatientDetails), StatusCodes.Status200OK)]
    public async Task<IResult> GetOwnAsync(CancellationToken cancellationToken)
    {
        var result = await patientService.GetOwnRecordAsync(cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("me/medical-history")]
    [Authorize(Policy = Policies.PatientOnly)]
    [ProducesResponseType(typeof(PagedResult<MedicalHistoryEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IResult> GetOwnMedicalHistoryAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await patientService.GetOwnMedicalHistoryAsync(
            new PageRequest { Page = page, PageSize = pageSize },
            cancellationToken);
        return result.ToHttpResult();
    }
}
