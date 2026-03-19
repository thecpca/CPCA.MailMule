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
        var mailbox = await this.GetIncomingMailboxByMessageIdAsync(messageId, cancellationToken);

        using var client = await ConnectAsync(mailbox, cancellationToken);
        var sourceFolder = await OpenSourceFolderAsync(client, mailbox, FolderAccess.ReadOnly, cancellationToken);

        var uniqueId = new UniqueId(messageId.Uid);
        return await sourceFolder.GetMessageAsync(uniqueId, cancellationToken);
    }

    public async Task RouteToJunkAsync(MessageId messageId, CancellationToken cancellationToken = default)
    {
        var sourceMailbox = await this.GetIncomingMailboxByMessageIdAsync(messageId, cancellationToken);

        if (String.IsNullOrWhiteSpace(sourceMailbox.TrashFolderPath))
        {
            throw new InvalidOperationException("Junk folder path is not configured for the source mailbox.");
        }

        using var sourceClient = await ConnectAsync(sourceMailbox, cancellationToken);
        var sourceFolder = await OpenSourceFolderAsync(sourceClient, sourceMailbox, FolderAccess.ReadWrite, cancellationToken);
        var junkFolder = await sourceClient.GetFolderAsync(sourceMailbox.TrashFolderPath, cancellationToken);

        var uid = new UniqueId(messageId.Uid);
        await sourceFolder.MoveToAsync(uid, junkFolder, cancellationToken);

        logger.LogInformation("Moved message {MessageUid} from mailbox {MailboxId} to junk folder.", messageId.Uid, sourceMailbox.Id);
    }

    public async Task RouteToMailboxAsync(MessageId messageId, MailboxId mailboxId, CancellationToken cancellationToken = default)
    {
        var sourceMailbox = await this.GetIncomingMailboxByMessageIdAsync(messageId, cancellationToken);
        var destinationMailbox = await this.GetOutgoingMailboxByIdAsync(mailboxId, cancellationToken);

        using var sourceClient = await ConnectAsync(sourceMailbox, cancellationToken);
        var sourceFolder = await OpenSourceFolderAsync(sourceClient, sourceMailbox, FolderAccess.ReadWrite, cancellationToken);
        var uid = new UniqueId(messageId.Uid);

        // Read from source then append to destination (cross-account move semantics).
        var message = await sourceFolder.GetMessageAsync(uid, cancellationToken);

        using var destinationClient = await ConnectAsync(destinationMailbox, cancellationToken);
        var destinationFolder = String.IsNullOrWhiteSpace(destinationMailbox.OutboxFolderPath)
            ? destinationClient.Inbox
            : await destinationClient.GetFolderAsync(destinationMailbox.OutboxFolderPath, cancellationToken);

        await destinationFolder.AppendAsync(new AppendRequest(message), cancellationToken);

        await this.RemoveFromSourceMailboxAsync(sourceFolder, sourceClient, sourceMailbox, uid, cancellationToken);

        logger.LogInformation(
            "Routed message {MessageUid} from source mailbox {SourceMailboxId} to destination mailbox {DestinationMailboxId}.",
            messageId.Uid,
            sourceMailbox.Id,
            destinationMailbox.Id);
    }

    private async Task<IReadOnlyList<MessageHeader>> GetMailboxHeadersAsync(MailboxConfig mailbox, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(mailbox, cancellationToken);
        var sourceFolder = await OpenSourceFolderAsync(client, mailbox, FolderAccess.ReadOnly, cancellationToken);

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

    private static async Task<IMailFolder> OpenSourceFolderAsync(ImapClient client, MailboxConfig mailbox, FolderAccess access, CancellationToken cancellationToken)
    {
        var folder = String.IsNullOrWhiteSpace(mailbox.InboxFolderPath)
            ? client.Inbox
            : await client.GetFolderAsync(mailbox.InboxFolderPath, cancellationToken);

        await folder.OpenAsync(access, cancellationToken);

        return folder;
    }

    private async Task<MailboxConfig> GetIncomingMailboxByMessageIdAsync(MessageId messageId, CancellationToken cancellationToken)
    {
        var mailboxEntityId = FromMailboxId(messageId.Mailbox);
        var mailbox = await mailboxConfigRepository.GetByIdAsync(mailboxEntityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mailbox with ID {mailboxEntityId} was not found.");

        if (!mailbox.IsActive)
        {
            throw new InvalidOperationException($"Mailbox with ID {mailboxEntityId} is inactive.");
        }

        if (mailbox.MailboxType != MailboxType.Incoming)
        {
            throw new InvalidOperationException($"Mailbox with ID {mailboxEntityId} is not an incoming mailbox.");
        }

        return mailbox;
    }

    private async Task<MailboxConfig> GetOutgoingMailboxByIdAsync(MailboxId mailboxId, CancellationToken cancellationToken)
    {
        var mailboxEntityId = FromMailboxId(mailboxId);
        var mailbox = await mailboxConfigRepository.GetByIdAsync(mailboxEntityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mailbox with ID {mailboxEntityId} was not found.");

        if (!mailbox.IsActive)
        {
            throw new InvalidOperationException($"Mailbox with ID {mailboxEntityId} is inactive.");
        }

        if (mailbox.MailboxType != MailboxType.Outgoing)
        {
            throw new InvalidOperationException($"Mailbox with ID {mailboxEntityId} is not an outgoing mailbox.");
        }

        return mailbox;
    }

    private async Task RemoveFromSourceMailboxAsync(
        IMailFolder sourceFolder,
        ImapClient sourceClient,
        MailboxConfig sourceMailbox,
        UniqueId uid,
        CancellationToken cancellationToken)
    {
        if (sourceMailbox.DeleteMessage)
        {
            await sourceFolder.AddFlagsAsync(uid, MessageFlags.Deleted, true, cancellationToken);
            await sourceFolder.ExpungeAsync(cancellationToken);
            return;
        }

        if (!String.IsNullOrWhiteSpace(sourceMailbox.TrashFolderPath))
        {
            var archiveFolder = await sourceClient.GetFolderAsync(sourceMailbox.TrashFolderPath, cancellationToken);
            await sourceFolder.MoveToAsync(uid, archiveFolder, cancellationToken);
            return;
        }

        throw new InvalidOperationException(
            $"DeleteMessage is false but mailbox {sourceMailbox.Id} does not have a Trash/Archive folder path configured.");
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
