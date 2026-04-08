using CPCA.MailMule.Backend.Services;
using CPCA.MailMule.Dtos;
using CPCA.MailMule.Services;
using Microsoft.AspNetCore.Mvc;

namespace CPCA.MailMule.Backend.Controllers;

[Route("session/{kingdom}")]
public sealed class SessionController(
    IKingOfTheHillService service,
    ILogger<AdminApiLog> logger) : BackendControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SessionStatusDto>> GetStatusAsync(Kingdom kingdom, CancellationToken ct)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return new JsonResult(null)
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        var status = await service.GetSessionStatusAsync(kingdom, ct);
        return Ok(status);
    }

    [HttpPost("claim")]
    public async Task<ActionResult<SessionClaimResult>> ClaimAsync(Kingdom kingdom, CancellationToken ct)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return new JsonResult(null)
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        var userId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
            ?? User.Identity?.Name
            ?? "unknown";
        var userName = User.Identity?.Name ?? "Unknown";

        var result = await service.ClaimKingshipAsync(kingdom, userId, userName, ct);

        if (!result.Success)
        {
            logger.LogWarning(
                "User {UserId} ({UserName}) denied king of the hill for {Kingdom}. Current king: {CurrentKing}",
                userId,
                userName,
                kingdom,
                result.CurrentKingUserName);
        }

        return Ok(result);
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> HeartbeatAsync(Kingdom kingdom, CancellationToken ct)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return new JsonResult(null)
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        await service.RecordActivityAsync(kingdom, ct);
        return NoContent();
    }

    [HttpPost("release")]
    public async Task<IActionResult> ReleaseAsync(Kingdom kingdom, CancellationToken ct)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return new JsonResult(null)
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        await service.ReleaseKingshipAsync(kingdom, ct);

        logger.LogInformation(
            "User {User} released king of the hill for {Kingdom}",
            GetCurrentUserName(),
            kingdom);

        return NoContent();
    }
}
