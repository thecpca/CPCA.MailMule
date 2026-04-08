using CPCA.MailMule.Backend.Services;
using CPCA.MailMule.Dtos;
using CPCA.MailMule.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPCA.MailMule.Backend.Controllers;

[Authorize(Policy = "Admin")]
[Route("admin/outgoing")]
public sealed class OutgoingMailboxesController(
    IMailboxConfigService service,
    ILogger<AdminApiLog> logger) : BackendControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MailboxConfigDto>>> ListAsync(CancellationToken ct)
    {
        var mailboxes = await service.GetMailboxesByTypeAsync("Outgoing", ct);
        return Ok(mailboxes);
    }

    [HttpPost]
    public async Task<ActionResult<Int64>> CreateAsync(CreateMailboxConfigDto dto, CancellationToken ct)
    {
        var id = await service.CreateMailboxAsync(dto with { MailboxType = "Outgoing" }, ct);

        logger.LogInformation(
            "Admin mailbox configuration created by {User} for {MailboxType} mailbox {MailboxId} ({DisplayName})",
            GetCurrentUserName(),
            "Outgoing",
            id,
            dto.DisplayName);

        return Created($"/admin/outgoing/{id}", id);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<MailboxConfigDto>> GetAsync(Int64 id, CancellationToken ct)
    {
        var mailbox = await service.GetMailboxAsync(id, ct);
        return mailbox == null ? NotFound() : Ok(mailbox);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync(Int64 id, UpdateMailboxConfigDto dto, CancellationToken ct)
    {
        if (dto.Id != id)
        {
            return BadRequest("ID mismatch");
        }

        var existingMailbox = await service.GetMailboxAsync(id, ct);
        if (existingMailbox == null)
        {
            return NotFound();
        }

        var changedFields = MailboxChangeTracking.GetChangedFields(existingMailbox, dto);

        try
        {
            await service.UpdateMailboxAsync(dto, ct);

            foreach (var fieldName in changedFields)
            {
                logger.LogInformation(
                    "Admin mailbox configuration field changed by {User}: {MailboxType} mailbox {MailboxId} {FieldName}",
                    GetCurrentUserName(),
                    "Outgoing",
                    id,
                    fieldName);
            }

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync(Int64 id, CancellationToken ct)
    {
        try
        {
            var mailbox = await service.GetMailboxAsync(id, ct);
            await service.DeleteMailboxAsync(id, ct);

            logger.LogInformation(
                "Admin mailbox configuration deleted by {User} for {MailboxType} mailbox {MailboxId} ({DisplayName})",
                GetCurrentUserName(),
                "Outgoing",
                id,
                mailbox?.DisplayName ?? String.Empty);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("test-connection")]
    public async Task<ActionResult<MailboxConnectionTestResult>> TestConnectionAsync(MailboxConnectionTestRequest request, CancellationToken ct)
    {
        var result = await service.TestConnectionAsync(request, ct);
        return Ok(result);
    }
}
