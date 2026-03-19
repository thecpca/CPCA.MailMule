using CPCA.MailMule.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPCA.MailMule.ImapService.Controllers;

[Authorize]
[ApiController]
[Route("api/messages")]
public sealed class MessagesController(IMailboxService mailboxService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MessageHeader>>> GetHeaders(CancellationToken cancellationToken)
    {
        var headers = await mailboxService.GetHeadersAsync(cancellationToken);
        return Ok(headers);
    }

    [HttpGet("{mailboxId:guid}/{uid:long}")]
    public async Task<IActionResult> GetMessage(Guid mailboxId, UInt32 uid, CancellationToken cancellationToken)
    {
        try
        {
            var message = await mailboxService.GetMessageAsync(
                new MessageId(new MailboxId(mailboxId), uid),
                cancellationToken);

            return Content(message.ToString(), "message/rfc822");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{mailboxId:guid}/{uid:long}/junk")]
    public async Task<IActionResult> RouteToJunk(Guid mailboxId, UInt32 uid, CancellationToken cancellationToken)
    {
        try
        {
            await mailboxService.RouteToJunkAsync(
                new MessageId(new MailboxId(mailboxId), uid),
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{mailboxId:guid}/{uid:long}/route/{destinationMailboxId:guid}")]
    public async Task<IActionResult> RouteToMailbox(Guid mailboxId, UInt32 uid, Guid destinationMailboxId, CancellationToken cancellationToken)
    {
        try
        {
            await mailboxService.RouteToMailboxAsync(
                new MessageId(new MailboxId(mailboxId), uid),
                new MailboxId(destinationMailboxId),
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
