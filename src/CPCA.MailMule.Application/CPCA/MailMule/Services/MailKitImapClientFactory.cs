namespace CPCA.MailMule.Services;

using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;

internal sealed class MailKitImapClientFactory(
    IStringProtector stringProtector,
    ILogger<MailKitImapClientFactory> logger) : IImapClientFactory
{
    public async Task<ImapClient> CreateConnectedClientAsync(MailboxConfig mailbox, CancellationToken cancellationToken = default)
    {
        var client = new ImapClient();

        try
        {
            var password = stringProtector.Unprotect(mailbox.EncryptedPassword);
            var security = ParseSecurity(mailbox.Security.ToString());

            await client.ConnectAsync(mailbox.ImapHost, mailbox.ImapPort, security, cancellationToken);
            await client.AuthenticateAsync(mailbox.Username, password, cancellationToken);

            logger.LogDebug(
                "Connected IMAP client to {Host}:{Port} for mailbox {MailboxId}",
                mailbox.ImapHost, mailbox.ImapPort, mailbox.Id);

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static SecureSocketOptions ParseSecurity(String security)
    {
        return security.ToLowerInvariant() switch
        {
            "ssl" or "tls" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.Auto,
        };
    }
}
