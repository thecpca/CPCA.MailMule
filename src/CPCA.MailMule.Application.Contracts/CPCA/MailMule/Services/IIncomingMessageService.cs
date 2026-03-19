namespace CPCA.MailMule.Services;

using CPCA.MailMule.Dtos;

public interface IIncomingMessageService
{
    Task<IReadOnlyList<IncomingMessageDto>> GetErrorMessagesAsync(CancellationToken cancellationToken = default);

    Task RequeueAsync(Int64 id, CancellationToken cancellationToken = default);

    Task DismissAsync(Int64 id, CancellationToken cancellationToken = default);
}
