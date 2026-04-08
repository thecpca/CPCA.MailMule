using CPCA.MailMule.Backend.Services;
using CPCA.MailMule.Dtos;
using CPCA.MailMule.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPCA.MailMule.Backend.Controllers;

[Authorize(Policy = "Admin")]
[Route("admin/errors")]
public sealed class ErrorsController(
    IIncomingMessageService service,
    ILogger<AdminApiLog> logger) : BackendControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<IncomingMessageDto>>> ListAsync(CancellationToken ct)
    {
        var errors = await service.GetErrorMessagesAsync(ct);
        return Ok(errors);
    }

    [HttpPost("{id:long}/requeue")]
    public async Task<IActionResult> RequeueAsync(Int64 id, CancellationToken ct)
    {
        await service.RequeueAsync(id, ct);

        logger.LogInformation(
            "Error message {MessageId} requeued by {User}",
            id,
            GetCurrentUserName());

        return NoContent();
    }

    [HttpPost("{id:long}/dismiss")]
    public async Task<IActionResult> DismissAsync(Int64 id, CancellationToken ct)
    {
        await service.DismissAsync(id, ct);

        logger.LogInformation(
            "Error message {MessageId} dismissed by {User}",
            id,
            GetCurrentUserName());

        return NoContent();
    }
}
