namespace CPCA.MailMule.Services;

using CPCA.MailMule.Dtos;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;

internal sealed class MailKitConnectionTester(ILogger<MailKitConnectionTester> logger) : IImapConnectionTester
{
    public async Task<MailboxConnectionTestResult> TestConnectionAsync(MailboxConnectionTestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var client = new ImapClient();

            var security = ParseSecurity(request.Security);
            await client.ConnectAsync(request.ImapHost, request.ImapPort, security, cancellationToken);
            await client.AuthenticateAsync(request.Username, request.Password, cancellationToken);

            var discoveredFolders = await GetFolderNamesAsync(client, cancellationToken);

            await client.DisconnectAsync(true, cancellationToken);

            logger.LogInformation("Connection test succeeded for {ImapHost}:{ImapPort}", request.ImapHost, request.ImapPort);

            return new MailboxConnectionTestResult(
                Success: true,
                Message: "Connection successful",
                FolderList: discoveredFolders.ToArray());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Connection test failed for {ImapHost}:{ImapPort}", request.ImapHost, request.ImapPort);

            return new MailboxConnectionTestResult(
                Success: false,
                Message: $"Connection test failed: {ex.Message}",
                FolderList: null);
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

    private static async Task<List<String>> GetFolderNamesAsync(ImapClient client, CancellationToken cancellationToken)
    {
        var folders = new List<String> { client.Inbox.FullName };

        if (client.PersonalNamespaces.Count == 0)
        {
            return folders;
        }

        try
        {
            var root = client.GetFolder(client.PersonalNamespaces[0]);
            var subfolders = await root.GetSubfoldersAsync(false, cancellationToken);
            folders.AddRange(subfolders.Select(x => x.FullName));
        }
        catch
        {
            // Folder discovery is best-effort for connection testing.
        }

        return folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
