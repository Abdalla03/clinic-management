using ClinicManagement.Api.Authorization;
using ClinicManagement.Api.Infrastructure;
using ClinicManagement.Application.Appointments;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Domain.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagement.Api.Controllers;

[Route("api/appointments")]
public sealed class AppointmentsController(IAppointmentService appointmentService) : ClinicControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(PagedResult<AppointmentSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> SearchAsync(
        [FromQuery] Guid? doctorId = null,
        [FromQuery] Guid? patientId = null,
        [FromQuery] AppointmentStatus? status = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await appointmentService.SearchAppointmentsAsync(
            new AppointmentSearchQuery
            {
                DoctorId = doctorId,
                PatientId = patientId,
                Status = status,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.PatientOnly)]
    [ProducesResponseType(typeof(PagedResult<AppointmentSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> GetOwnAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await appointmentService.GetOwnAppointmentsAsync(
            new PageRequest { Page = page, PageSize = pageSize },
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpGet("{appointmentId:guid}")]
    [Authorize(Policy = Policies.AuthenticatedUser)]
    [ProducesResponseType(typeof(AppointmentDetails), StatusCodes.Status200OK)]
    public async Task<IResult> GetAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var result = await appointmentService.GetAppointmentAsync(appointmentId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(AppointmentDetails), StatusCodes.Status201Created)]
    public async Task<IResult> BookAsync(
        [FromBody] BookAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await appointmentService.BookAppointmentAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.Created($"/api/appointments/{result.Value!.Id}", result.Value)
            : result.ToHttpResult();
    }

    [HttpPost("me")]
    [Authorize(Policy = Policies.PatientOnly)]
    [ProducesResponseType(typeof(AppointmentDetails), StatusCodes.Status201Created)]
    public async Task<IResult> BookMyAsync(
        [FromBody] BookMyAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await appointmentService.BookMyAppointmentAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.Created($"/api/appointments/{result.Value!.Id}", result.Value)
            : result.ToHttpResult();
    }

    [HttpPost("me/{appointmentId:guid}/cancel")]
    [Authorize(Policy = Policies.PatientOnly)]
    [ProducesResponseType(typeof(AppointmentDetails), StatusCodes.Status200OK)]
    public async Task<IResult> CancelMyAsync(
        Guid appointmentId,
        [FromBody] CancelAppointmentRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var result = await appointmentService.CancelOwnAppointmentAsync(
            appointmentId,
            request ?? new CancelAppointmentRequest(),
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpPut("{appointmentId:guid}")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(AppointmentDetails), StatusCodes.Status200OK)]
    public async Task<IResult> RescheduleAsync(
        Guid appointmentId,
        [FromBody] RescheduleAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await appointmentService.RescheduleAppointmentAsync(appointmentId, request, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("{appointmentId:guid}/confirm")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(AppointmentDetails), StatusCodes.Status200OK)]
    public async Task<IResult> ConfirmAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var result = await appointmentService.ConfirmAppointmentAsync(appointmentId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("{appointmentId:guid}/complete")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(AppointmentDetails), StatusCodes.Status200OK)]
    public async Task<IResult> CompleteAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var result = await appointmentService.CompleteAppointmentAsync(appointmentId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("{appointmentId:guid}/no-show")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(AppointmentDetails), StatusCodes.Status200OK)]
    public async Task<IResult> MarkNoShowAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var result = await appointmentService.MarkNoShowAsync(appointmentId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("{appointmentId:guid}/cancel")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(AppointmentDetails), StatusCodes.Status200OK)]
    public async Task<IResult> CancelAsync(
        Guid appointmentId,
        [FromBody] CancelAppointmentRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var result = await appointmentService.CancelAppointmentAsync(
            appointmentId,
            request ?? new CancelAppointmentRequest(),
            cancellationToken);

        return result.ToHttpResult();
    }
}
