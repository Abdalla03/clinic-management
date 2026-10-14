using ClinicManagement.Api.Authorization;
using ClinicManagement.Api.Infrastructure;
using ClinicManagement.Application.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagement.Api.Controllers;

[Route("api/doctors/{doctorId:guid}")]
public sealed class DoctorsController(IAppointmentService appointmentService) : ClinicControllerBase
{
    [HttpGet("~/api/doctors")]
    [Authorize(Policy = Policies.AuthenticatedUser)]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorSummary>), StatusCodes.Status200OK)]
    public async Task<IResult> ListAsync(CancellationToken cancellationToken)
    {
        var result = await appointmentService.ListDoctorsAsync(cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("availability")]
    [Authorize(Policy = Policies.AuthenticatedUser)]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilitySlot>), StatusCodes.Status200OK)]
    public async Task<IResult> GetAvailabilityAsync(
        Guid doctorId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var result = await appointmentService.GetAvailabilityAsync(doctorId, fromUtc, toUtc, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("schedule")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorScheduleResponse>), StatusCodes.Status200OK)]
    public async Task<IResult> GetScheduleAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var result = await appointmentService.GetScheduleAsync(doctorId, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost("schedule")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(typeof(DoctorScheduleResponse), StatusCodes.Status201Created)]
    public async Task<IResult> AddScheduleAsync(
        Guid doctorId,
        [FromBody] UpsertDoctorScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await appointmentService.AddScheduleAsync(doctorId, request, cancellationToken);

        return result.Succeeded
            ? Results.Created($"/api/doctors/{doctorId}/schedule/{result.Value!.Id}", result.Value)
            : result.ToHttpResult();
    }

    [HttpDelete("schedule/{scheduleId:guid}")]
    [Authorize(Policy = Policies.ClinicalStaff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> DeactivateScheduleAsync(
        Guid doctorId,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        var result = await appointmentService.DeactivateScheduleAsync(doctorId, scheduleId, cancellationToken);
        return result.ToHttpResult();
    }
}
