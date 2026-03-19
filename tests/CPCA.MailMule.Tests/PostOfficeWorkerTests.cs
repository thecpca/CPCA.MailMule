using CPCA.MailMule.Dtos;
using CPCA.MailMule.Frontend.Services;

namespace CPCA.MailMule.Tests;

public sealed class PostOfficeWorkerTests
{
    [Fact]
    public async Task RefreshAsync_SyncsInboxAndOutgoingMailboxes()
    {
        // Arrange
        var mailboxId = CreateMailboxId(1);
        var headers = new[]
        {
            new MessageHeader(
                new MessageId(mailboxId, 101),
                DateTimeOffset.UtcNow,
                new FullEmail(new EmailAddress("inbox@example.com"), new Name("Inbox", "User")),
                "Subject")
        };

        var outgoingMailbox = new MailboxConfigDto(
            Id: 42,
            DisplayName: "Dest A",
            ImapHost: "imap.example.com",
            ImapPort: 993,
            MailboxType: "Outgoing",
            Security: "Auto",
            Username: "dest@example.com",
            InboxFolderPath: null,
            OutboxFolderPath: null,
            SentFolderPath: null,
            ArchiveFolderPath: null,
            JunkFolderPath: null,
            PollIntervalSeconds: 60,
            DeleteMessage: false,
            IsActive: true,
            LastPolledUtc: null,
            SortOrder: 2);

        var incomingMailbox = outgoingMailbox with
        {
            Id = 5,
            DisplayName = "Source A",
            MailboxType = "Incoming",
            PollIntervalSeconds = 20,
            SortOrder = 0,
        };

        var messageApiClient = new FakeMessageApiClient { Headers = headers };
        var mailboxConfigApiClient = new FakeMailboxConfigApiClient();
        mailboxConfigApiClient.MailboxesByType["Outgoing"] = [outgoingMailbox];
        mailboxConfigApiClient.MailboxesByType["Incoming"] = [incomingMailbox];
        var postOffice = new PostOffice();
        var worker = new PostOfficeWorker(messageApiClient, mailboxConfigApiClient, new FakeUserSettingsApiClient(), postOffice);

        // Act
        await worker.RefreshAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(headers.Single().Id, Assert.Single(postOffice.Inbox).MessageId);
        Assert.Equal("Dest A", worker.GetMailboxDisplayName(CreateMailboxId(42)));
    }

    [Fact]
    public async Task RefreshAsync_UpdatesUndoWindowSecondsFromSettings()
    {
        // Arrange
        var messageApiClient = new FakeMessageApiClient();
        var mailboxConfigApiClient = new FakeMailboxConfigApiClient();
        var settingsClient = new FakeUserSettingsApiClient
        {
            Settings = new UserSettingsDto(UndoWindowSeconds: 42, PageSize: 50)
        };
        var postOffice = new PostOffice();
        Assert.Equal(15, postOffice.UndoWindowSeconds); // default

        var worker = new PostOfficeWorker(messageApiClient, mailboxConfigApiClient, settingsClient, postOffice);

        // Act
        await worker.RefreshAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(42, postOffice.UndoWindowSeconds);
    }

    [Fact]
    public async Task ProcessDueEnvelopesAsync_SuccessfulDelivery_RemovesEnvelopeAndRaisesSuccessNotification()
    {
        // Arrange
        var postOffice = new PostOffice { UndoWindowSeconds = 15 };
        var message = CreateMessage(1, 201, "Route me", DateTimeOffset.UtcNow);
        var destination = CreateMailboxId(7);

        await postOffice.AddMessageAsync(message, TestContext.Current.CancellationToken);
        await postOffice.QueueDeliveryAsync(message.MessageId, destination, TestContext.Current.CancellationToken);
        await postOffice.ExecuteNowAsync(message.MessageId, TestContext.Current.CancellationToken);

        var messageApiClient = new FakeMessageApiClient { RouteToMailboxResult = true };
        var mailboxConfigApiClient = new FakeMailboxConfigApiClient();
        var worker = new PostOfficeWorker(messageApiClient, mailboxConfigApiClient, new FakeUserSettingsApiClient(), postOffice);

        PostOfficeWorkerNotification? notification = null;
        worker.NotificationRaised += value => notification = value;

        // Act
        await worker.ProcessDueEnvelopesAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(postOffice.Outbox);
        Assert.Empty(postOffice.Inbox);
        Assert.Equal(message.MessageId, messageApiClient.LastRouteToMailboxMessageId);
        Assert.Equal(PostOfficeWorkerNotificationLevel.Success, notification?.Level);
    }

    [Fact]
    public async Task ProcessDueEnvelopesAsync_FailedJunkRoute_ReturnsMessageToInboxAndRaisesErrorNotification()
    {
        // Arrange
        var postOffice = new PostOffice { UndoWindowSeconds = 15 };
        var message = CreateMessage(1, 301, "Junk me", DateTimeOffset.UtcNow);

        await postOffice.AddMessageAsync(message, TestContext.Current.CancellationToken);
        await postOffice.QueueJunkAsync(message, TestContext.Current.CancellationToken);
        await postOffice.ExecuteNowAsync(message.MessageId, TestContext.Current.CancellationToken);

        var messageApiClient = new FakeMessageApiClient { RouteToJunkResult = false };
        var mailboxConfigApiClient = new FakeMailboxConfigApiClient();
        var worker = new PostOfficeWorker(messageApiClient, mailboxConfigApiClient, new FakeUserSettingsApiClient(), postOffice);

        PostOfficeWorkerNotification? notification = null;
        worker.NotificationRaised += value => notification = value;

        // Act
        await worker.ProcessDueEnvelopesAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(postOffice.Outbox);
        Assert.Equal(message.MessageId, Assert.Single(postOffice.Inbox).MessageId);
        Assert.Equal(message.MessageId, messageApiClient.LastRouteToJunkMessageId);
        Assert.Equal(PostOfficeWorkerNotificationLevel.Error, notification?.Level);
    }

    private static MessageHeaderDto CreateMessage(Int64 mailboxValue, UInt32 uid, String subject, DateTimeOffset receivedAt)
    {
        return new MessageHeaderDto
        {
            MessageId = new MessageId(CreateMailboxId(mailboxValue), uid),
            From = new FullEmail(new EmailAddress("operator@example.com"), new Name("Mail", "Mule")),
            Subject = subject,
            DateSent = receivedAt,
            DateReceived = receivedAt,
            To = []
        };
    }

    private static MailboxId CreateMailboxId(Int64 value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new MailboxId(new Guid(bytes));
    }

    private sealed class FakeMessageApiClient : IMessageApiClient
    {
        public IReadOnlyList<MessageHeader> Headers { get; init; } = [];
        public MessageBodyDto? Message { get; init; }
        public Boolean RouteToJunkResult { get; init; } = true;
        public Boolean RouteToMailboxResult { get; init; } = true;
        public MessageId? LastRouteToJunkMessageId { get; private set; }
        public MessageId? LastRouteToMailboxMessageId { get; private set; }

        public Task<IReadOnlyList<MessageHeader>> GetHeadersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Headers);

        public Task<MessageBodyDto?> GetMessageAsync(MessageId messageId, CancellationToken cancellationToken = default)
            => Task.FromResult(Message);

        public Task<Boolean> RouteToJunkAsync(MessageId messageId, CancellationToken cancellationToken = default)
        {
            LastRouteToJunkMessageId = messageId;
            return Task.FromResult(RouteToJunkResult);
        }

        public Task<Boolean> RouteToMailboxAsync(MessageId messageId, MailboxId destinationMailboxId, CancellationToken cancellationToken = default)
        {
            LastRouteToMailboxMessageId = messageId;
            return Task.FromResult(RouteToMailboxResult);
        }
    }

    private sealed class FakeMailboxConfigApiClient : IMailboxConfigApiClient
    {
        public Dictionary<String, IEnumerable<MailboxConfigDto>> MailboxesByType { get; } = [];

        public Task<IEnumerable<MailboxConfigDto>?> GetMailboxesByTypeAsync(String mailboxType, CancellationToken cancellationToken = default)
            => Task.FromResult(MailboxesByType.TryGetValue(mailboxType, out var mailboxes) ? mailboxes : Enumerable.Empty<MailboxConfigDto>());

        public Task<MailboxConfigDto?> GetMailboxAsync(Int64 id, String mailboxType, CancellationToken cancellationToken = default)
            => Task.FromResult<MailboxConfigDto?>(null);

        public Task<Boolean> CreateMailboxAsync(CreateMailboxConfigDto dto, String mailboxType, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<Boolean> UpdateMailboxAsync(UpdateMailboxConfigDto dto, String mailboxType, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<Boolean> DeleteMailboxAsync(Int64 id, String mailboxType, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<MailboxConnectionTestResult?> TestConnectionAsync(MailboxConnectionTestRequest request, String mailboxType, CancellationToken cancellationToken = default)
            => Task.FromResult<MailboxConnectionTestResult?>(null);
    }

    private sealed class FakeUserSettingsApiClient : IUserSettingsApiClient
    {
        public UserSettingsDto Settings { get; init; } = new(UndoWindowSeconds: 15, PageSize: 25);

        public Task<UserSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<UserSettingsDto?>(Settings);

        public Task<Boolean> UpdateAsync(UserSettingsDto dto, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
