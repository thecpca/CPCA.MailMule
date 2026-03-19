namespace CPCA.MailMule.Frontend.Services;

public interface IMessageApiClient
{
    Task<IReadOnlyList<MessageHeader>> GetHeadersAsync(CancellationToken cancellationToken = default);
    Task<MessageBodyDto?> GetMessageAsync(MessageId messageId, CancellationToken cancellationToken = default);
    Task<Boolean> RouteToJunkAsync(MessageId messageId, CancellationToken cancellationToken = default);
    Task<Boolean> RouteToMailboxAsync(MessageId messageId, MailboxId destinationMailboxId, CancellationToken cancellationToken = default);
}