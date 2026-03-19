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
        var message = await mailboxService.GetMessageAsync(
            new MessageId(new MailboxId(mailboxId), uid),
            cancellationToken);

        return Content(message.ToString(), "message/rfc822");
    }
}
