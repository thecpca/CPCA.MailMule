using CPCA.MailMule.Backend.Services;
using CPCA.MailMule.Dtos;
using CPCA.MailMule.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPCA.MailMule.Backend.Controllers;

[Route("admin")]
public sealed class SettingsController(
    IUserSettingsService userSettingsService,
    IApplicationSettingsService applicationSettingsService,
    ILogger<AdminApiLog> logger) : BackendControllerBase
{
    [HttpGet("settings")]
    [Authorize(Policy = "Operator")]
    public async Task<ActionResult<UserSettingsDto>> GetUserSettingsAsync(CancellationToken ct)
    {
        var settings = await userSettingsService.GetAsync(ct);
        return Ok(settings);
    }

    [HttpPut("settings")]
    [Authorize(Policy = "Operator")]
    public async Task<IActionResult> UpdateUserSettingsAsync(UserSettingsDto dto, CancellationToken ct)
    {
        await userSettingsService.UpdateAsync(dto, ct);

        logger.LogInformation(
            "User settings updated by {User}: UndoWindowSeconds={UndoWindowSeconds}, PageSize={PageSize}",
            GetCurrentUserName(),
            dto.UndoWindowSeconds,
            dto.PageSize);

        return NoContent();
    }

    [HttpGet("app-settings")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ApplicationSettingsDto>> GetApplicationSettingsAsync(CancellationToken ct)
    {
        var settings = await applicationSettingsService.GetAsync(ct);
        return Ok(settings);
    }

    [HttpPut("app-settings")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> UpdateApplicationSettingsAsync(ApplicationSettingsDto dto, CancellationToken ct)
    {
        await applicationSettingsService.UpdateAsync(dto, ct);

        logger.LogInformation(
            "Application settings updated by {User}: InactivityTimeoutMinutes={InactivityTimeoutMinutes}",
            GetCurrentUserName(),
            dto.InactivityTimeoutMinutes);

        return NoContent();
    }
}
