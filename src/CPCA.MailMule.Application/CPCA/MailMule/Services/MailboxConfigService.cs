namespace CPCA.MailMule.Services;

using CPCA.MailMule.Dtos;
using CPCA.MailMule.Repositories;
using Microsoft.Extensions.Logging;

internal sealed class MailboxConfigService : IMailboxConfigService
{
    private readonly MailboxConfigRepository repository;
    private readonly IStringProtector stringProtector;
    private readonly ILogger<MailboxConfigService> logger;

    public MailboxConfigService(
        MailboxConfigRepository repository,
        IStringProtector stringProtector,
        ILogger<MailboxConfigService> logger)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.stringProtector = stringProtector ?? throw new ArgumentNullException(nameof(stringProtector));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<MailboxConfigDto>> GetMailboxesByTypeAsync(String mailboxType, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Fetching mailboxes by type: {MailboxType}", mailboxType);

        var mailboxes = await this.repository.GetByMailboxTypeAsync(mailboxType, cancellationToken);
        return mailboxes.Select(this.MapToDto).ToList();
    }

    public async Task<MailboxConfigDto?> GetMailboxAsync(Int64 id, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Fetching mailbox: {MailboxId}", id);

        var mailbox = await this.repository.GetByIdAsync(id, cancellationToken);
        return mailbox == null ? null : this.MapToDto(mailbox);
    }

    public async Task<Int64> CreateMailboxAsync(CreateMailboxConfigDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        this.logger.LogInformation("Creating mailbox: {DisplayName}", request.DisplayName);

        // Check for duplicate display name
        var exists = await this.repository.ExistsByDisplayNameAsync(request.DisplayName, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"A mailbox with display name '{request.DisplayName}' already exists.");
        }

        var encryptedPassword = this.stringProtector.Protect(request.Password);

        var mailbox = new MailboxConfig
        {
            DisplayName = request.DisplayName,
            ImapHost = request.ImapHost,
            ImapPort = request.ImapPort,
            MailboxType = Enum.Parse<MailboxType>(request.MailboxType),
            Security = Enum.Parse<MailboxSecurity>(request.Security),
            Username = request.Username,
            EncryptedPassword = encryptedPassword,
            InboxFolderPath = request.InboxFolderPath,
            OutboxFolderPath = request.OutboxFolderPath,
            SentFolderPath = request.SentFolderPath,
            TrashFolderPath = request.TrashFolderPath,
            PollIntervalSeconds = request.PollIntervalSeconds,
            DeleteMessage = request.DeleteMessage,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
        };

        await this.repository.AddAsync(mailbox, cancellationToken);

        this.logger.LogInformation("Mailbox created with ID: {MailboxId}", mailbox.Id);
        return mailbox.Id;
    }

    public async Task UpdateMailboxAsync(UpdateMailboxConfigDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        this.logger.LogInformation("Updating mailbox: {MailboxId}", request.Id);

        var mailbox = await this.repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Mailbox with ID {request.Id} not found.");

        mailbox.DisplayName = request.DisplayName;
        mailbox.ImapHost = request.ImapHost;
        mailbox.ImapPort = request.ImapPort;
        mailbox.MailboxType = Enum.Parse<MailboxType>(request.MailboxType);
        mailbox.Security = Enum.Parse<MailboxSecurity>(request.Security);
        mailbox.Username = request.Username;

        if (!String.IsNullOrEmpty(request.Password))
        {
            mailbox.EncryptedPassword = this.stringProtector.Protect(request.Password);
        }

        mailbox.InboxFolderPath = request.InboxFolderPath;
        mailbox.OutboxFolderPath = request.OutboxFolderPath;
        mailbox.SentFolderPath = request.SentFolderPath;
        mailbox.TrashFolderPath = request.TrashFolderPath;
        mailbox.PollIntervalSeconds = request.PollIntervalSeconds;
        mailbox.DeleteMessage = request.DeleteMessage;
        mailbox.IsActive = request.IsActive;
        mailbox.SortOrder = request.SortOrder;

        this.repository.Update(mailbox);
        await this.repository.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Mailbox updated: {MailboxId}", request.Id);
    }

    public async Task DeleteMailboxAsync(Int64 id, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Deleting mailbox: {MailboxId}", id);

        var mailbox = await this.repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Mailbox with ID {id} not found.");

        this.repository.Remove(mailbox);
        await this.repository.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Mailbox deleted: {MailboxId}", id);
    }

    public async Task<MailboxConnectionTestResult> TestConnectionAsync(MailboxConnectionTestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        this.logger.LogInformation("Testing connection to {ImapHost}:{ImapPort}", request.ImapHost, request.ImapPort);

        try
        {
            // Placeholder for actual IMAP connection test
            // In Phase 3, this will use the IMailboxService abstraction with MailKit
            // For now, just validate the basic parameters
            if (String.IsNullOrWhiteSpace(request.ImapHost))
            {
                return new MailboxConnectionTestResult(false, "IMAP host is required", null);
            }

            if (request.ImapPort <= 0 || request.ImapPort > 65535)
            {
                return new MailboxConnectionTestResult(false, "IMAP port must be between 1 and 65535", null);
            }

            if (String.IsNullOrWhiteSpace(request.Username))
            {
                return new MailboxConnectionTestResult(false, "Username is required", null);
            }

            if (String.IsNullOrWhiteSpace(request.Password))
            {
                return new MailboxConnectionTestResult(false, "Password is required", null);
            }

            // TODO: In Phase 3, implement actual IMAP connection test using MailKit
            this.logger.LogInformation("Connection test parameters validated for {ImapHost}", request.ImapHost);
            return new MailboxConnectionTestResult(true, "Connection test placeholder (actual test in Phase 3)", new[] { "INBOX" });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Connection test failed for {ImapHost}", request.ImapHost);
            return new MailboxConnectionTestResult(false, $"Connection test failed: {ex.Message}", null);
        }
    }

    private MailboxConfigDto MapToDto(MailboxConfig mailbox)
    {
        return new MailboxConfigDto(
            Id: mailbox.Id,
            DisplayName: mailbox.DisplayName,
            ImapHost: mailbox.ImapHost,
            ImapPort: mailbox.ImapPort,
            MailboxType: mailbox.MailboxType.ToString(),
            Security: mailbox.Security.ToString(),
            Username: mailbox.Username,
            InboxFolderPath: mailbox.InboxFolderPath,
            OutboxFolderPath: mailbox.OutboxFolderPath,
            SentFolderPath: mailbox.SentFolderPath,
            TrashFolderPath: mailbox.TrashFolderPath,
            PollIntervalSeconds: mailbox.PollIntervalSeconds,
            DeleteMessage: mailbox.DeleteMessage,
            IsActive: mailbox.IsActive,
            LastPolledUtc: mailbox.LastPolledUtc,
            SortOrder: mailbox.SortOrder
        );
    }
}
