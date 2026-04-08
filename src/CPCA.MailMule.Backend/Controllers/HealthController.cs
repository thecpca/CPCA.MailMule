using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CPCA.MailMule.Backend.Controllers;

[Route("health")]
public sealed class HealthController(HealthCheckService healthCheckService) : BackendControllerBase
{
    [HttpGet("live")]
    public async Task<IActionResult> GetLiveAsync(CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(check => check.Tags.Contains("live"), cancellationToken);
        return report.Status == HealthStatus.Healthy ? Ok() : StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    [HttpGet("ready")]
    public async Task<IActionResult> GetReadyAsync(CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);
        return report.Status == HealthStatus.Healthy ? Ok() : StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
