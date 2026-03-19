namespace CPCA.MailMule.Services;

using CPCA.MailMule.Dtos;

public interface IMailboxConfigService
{
    Task<IEnumerable<MailboxConfigDto>> GetMailboxesByTypeAsync(String mailboxType, CancellationToken cancellationToken = default);
    
    Task<MailboxConfigDto?> GetMailboxAsync(Int64 id, CancellationToken cancellationToken = default);
    
    Task<Int64> CreateMailboxAsync(CreateMailboxConfigDto request, CancellationToken cancellationToken = default);
    
    Task UpdateMailboxAsync(UpdateMailboxConfigDto request, CancellationToken cancellationToken = default);
    
    Task DeleteMailboxAsync(Int64 id, CancellationToken cancellationToken = default);
    
    Task<MailboxConnectionTestResult> TestConnectionAsync(MailboxConnectionTestRequest request, CancellationToken cancellationToken = default);
}
