namespace CPCA.MailMule.Services;

using MailKit.Net.Imap;

public interface IImapClientFactory
{
    Task<ImapClient> CreateConnectedClientAsync(MailboxConfig mailbox, CancellationToken cancellationToken = default);
}
