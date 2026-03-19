using CPCA.MailMule.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeKit;

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
    public async Task<ActionResult<MessageBodyDto>> GetMessage(Guid mailboxId, UInt32 uid, CancellationToken cancellationToken)
    {
        try
        {
            var message = await mailboxService.GetMessageAsync(
                new MessageId(new MailboxId(mailboxId), uid),
                cancellationToken);

            return Ok(new MessageBodyDto(
                HtmlBody: FindBody(message, "text/html"),
                TextBody: FindBody(message, "text/plain")));
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

    private static String? FindBody(MimeMessage message, String mimeType)
    {
        return message.BodyParts
            .OfType<TextPart>()
            .FirstOrDefault(part => String.Equals(part.ContentType.MimeType, mimeType, StringComparison.OrdinalIgnoreCase))
            ?.Text;
    }
}
