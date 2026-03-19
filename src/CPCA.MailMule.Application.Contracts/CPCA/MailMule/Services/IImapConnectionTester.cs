namespace CPCA.MailMule.Services;

using CPCA.MailMule.Dtos;

public interface IImapConnectionTester
{
    Task<MailboxConnectionTestResult> TestConnectionAsync(MailboxConnectionTestRequest request, CancellationToken cancellationToken = default);
}
