namespace CPCA.MailMule.Services;

using CPCA.MailMule.Repositories;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

internal sealed class MailKitMailboxService(
    MailboxConfigRepository mailboxConfigRepository,
    IStringProtector stringProtector,
    ILogger<MailKitMailboxService> logger) : IMailboxService
{
    private const Int32 MaxHeadersPerMailbox = 100;

    public async Task<IReadOnlyList<MessageHeader>> GetHeadersAsync(CancellationToken cancellationToken = default)
    {
        var incomingMailboxes = await mailboxConfigRepository.GetByMailboxTypeAsync("Incoming", cancellationToken);
        var activeMailboxes = incomingMailboxes.Where(x => x.IsActive).ToList();

        var headers = new List<MessageHeader>();

        foreach (var mailbox in activeMailboxes)
        {
            try
            {
                var mailboxHeaders = await this.GetMailboxHeadersAsync(mailbox, cancellationToken);
                headers.AddRange(mailboxHeaders);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch headers for mailbox {MailboxId}", mailbox.Id);
            }
        }

        return headers
            .OrderByDescending(x => x.Date)
            .ToList();
    }

    public async Task<MimeMessage> GetMessageAsync(MessageId messageId, CancellationToken cancellationToken = default)
    {
        var mailboxEntityId = FromMailboxId(messageId.Mailbox);

        var mailbox = await mailboxConfigRepository.GetByIdAsync(mailboxEntityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mailbox with ID {mailboxEntityId} was not found.");

        using var client = await ConnectAsync(mailbox, cancellationToken);
        var sourceFolder = await OpenSourceFolderAsync(client, mailbox, cancellationToken);

        var uniqueId = new UniqueId(messageId.Uid);
        return await sourceFolder.GetMessageAsync(uniqueId, cancellationToken);
    }

    public Task RouteToJunkAsync(MessageId messageId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("RouteToJunkAsync will be implemented in the next IMAP routing slice.");
    }

    public Task RouteToMailboxAsync(MessageId messageId, MailboxId mailboxId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("RouteToMailboxAsync will be implemented in the next IMAP routing slice.");
    }

    private async Task<IReadOnlyList<MessageHeader>> GetMailboxHeadersAsync(MailboxConfig mailbox, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(mailbox, cancellationToken);
        var sourceFolder = await OpenSourceFolderAsync(client, mailbox, cancellationToken);

        var allUids = await sourceFolder.SearchAsync(SearchQuery.All, cancellationToken);
        var limitedUids = allUids.TakeLast(MaxHeadersPerMailbox).ToList();

        if (limitedUids.Count == 0)
        {
            return [];
        }

        var summaries = await sourceFolder.FetchAsync(
            limitedUids,
            MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate,
            cancellationToken);

        var mailboxId = ToMailboxId(mailbox.Id);

        return summaries
            .Where(x => x.Envelope != null)
            .Select(x => new MessageHeader(
                Id: new MessageId(mailboxId, x.UniqueId.Id),
                Date: x.Envelope!.Date ?? x.InternalDate ?? DateTimeOffset.UtcNow,
                From: ToFullEmail(x.Envelope!.From.Mailboxes.FirstOrDefault()),
                Subject: x.Envelope!.Subject ?? String.Empty))
            .ToList();
    }

    private async Task<ImapClient> ConnectAsync(MailboxConfig mailbox, CancellationToken cancellationToken)
    {
        var client = new ImapClient();

        var password = stringProtector.Unprotect(mailbox.EncryptedPassword);
        var security = ParseSecurity(mailbox.Security.ToString());

        await client.ConnectAsync(mailbox.ImapHost, mailbox.ImapPort, security, cancellationToken);
        await client.AuthenticateAsync(mailbox.Username, password, cancellationToken);

        return client;
    }

    private static async Task<IMailFolder> OpenSourceFolderAsync(ImapClient client, MailboxConfig mailbox, CancellationToken cancellationToken)
    {
        var folder = String.IsNullOrWhiteSpace(mailbox.InboxFolderPath)
            ? client.Inbox
            : await client.GetFolderAsync(mailbox.InboxFolderPath, cancellationToken);

        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        return folder;
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

    private static FullEmail ToFullEmail(MailboxAddress? mailboxAddress)
    {
        if (mailboxAddress == null)
        {
            return new FullEmail(new EmailAddress(String.Empty), new Name(String.Empty, String.Empty));
        }

        var name = mailboxAddress.Name ?? String.Empty;
        var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstName = parts.Length > 0 ? parts[0] : String.Empty;
        var lastName = parts.Length > 1 ? parts[1] : String.Empty;

        return new FullEmail(
            Email: new EmailAddress(mailboxAddress.Address ?? String.Empty),
            Name: new Name(firstName, lastName));
    }

    private static MailboxId ToMailboxId(Int64 mailboxEntityId)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, mailboxEntityId);
        return new MailboxId(new Guid(bytes));
    }

    private static Int64 FromMailboxId(MailboxId mailboxId)
    {
        Span<byte> bytes = stackalloc byte[16];
        mailboxId.Value.TryWriteBytes(bytes);
        return BitConverter.ToInt64(bytes[..8]);
    }
}
