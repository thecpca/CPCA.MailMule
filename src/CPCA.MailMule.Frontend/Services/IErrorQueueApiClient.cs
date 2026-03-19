using CPCA.MailMule.Dtos;

namespace CPCA.MailMule.Frontend.Services;

public interface IErrorQueueApiClient
{
    Task<IReadOnlyList<IncomingMessageDto>> GetErrorsAsync(CancellationToken cancellationToken = default);

    Task<Boolean> RequeueAsync(Int64 id, CancellationToken cancellationToken = default);

    Task<Boolean> DismissAsync(Int64 id, CancellationToken cancellationToken = default);
}
