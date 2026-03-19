using MimeKit;

namespace CPCA.MailMule.Services;

public interface IMailboxService
{
    Task<IReadOnlyList<MessageHeader>> GetHeadersAsync(CancellationToken cancellationToken = default);

    Task<MimeMessage> GetMessageAsync(MessageId messageId, CancellationToken cancellationToken = default);

    Task RouteToJunkAsync(MessageId messageId, CancellationToken cancellationToken = default);

    Task RouteToMailboxAsync(MessageId messageId, MailboxId mailboxId, CancellationToken cancellationToken = default);
}
