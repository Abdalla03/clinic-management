using ClinicManagement.Api.Infrastructure;
using ClinicManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagement.Api.Controllers;

[Route("health")]
[AllowAnonymous]
public sealed class HealthController(ClinicDbContext dbContext) : ControllerBase
{
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "Healthy" });

    [HttpGet("ready")]
    public async Task<IActionResult> ReadyAsync(CancellationToken cancellationToken)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Ok(new { status = "Healthy", database = "Reachable" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy", database = "Unreachable" });
    }
}