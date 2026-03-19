using CPCA.MailMule.Dtos;

namespace CPCA.MailMule.Frontend.Services;

public interface IMailboxConfigApiClient
{
    Task<IEnumerable<MailboxConfigDto>?> GetMailboxesByTypeAsync(String mailboxType, CancellationToken cancellationToken = default);
    Task<MailboxConfigDto?> GetMailboxAsync(Int64 id, String mailboxType, CancellationToken cancellationToken = default);
    Task<Boolean> CreateMailboxAsync(CreateMailboxConfigDto dto, String mailboxType, CancellationToken cancellationToken = default);
    Task<Boolean> UpdateMailboxAsync(UpdateMailboxConfigDto dto, String mailboxType, CancellationToken cancellationToken = default);
    Task<Boolean> DeleteMailboxAsync(Int64 id, String mailboxType, CancellationToken cancellationToken = default);
    Task<MailboxConnectionTestResult?> TestConnectionAsync(MailboxConnectionTestRequest request, String mailboxType, CancellationToken cancellationToken = default);
}