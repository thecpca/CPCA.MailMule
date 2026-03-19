using CPCA.MailMule.Frontend.Services;

namespace CPCA.MailMule.Tests;

public sealed class PostOfficeTests
{
    [Fact]
    public async Task SyncInboxAsync_ReplacesInboxAndExcludesQueuedMessages()
    {
        // Arrange
        var postOffice = new PostOffice();
        var first = CreateMessage(1, 101, "First", DateTimeOffset.UtcNow.AddMinutes(-2));
        var second = CreateMessage(1, 102, "Second", DateTimeOffset.UtcNow.AddMinutes(-1));

        await postOffice.SyncInboxAsync([first, second], TestContext.Current.CancellationToken);
        await postOffice.QueueDeliveryAsync(first.MessageId, CreateMailboxId(99), TestContext.Current.CancellationToken);

        var refreshedFirst = CreateMessage(1, 101, "First", DateTimeOffset.UtcNow.AddMinutes(-2));
        var refreshedSecond = CreateMessage(1, 102, "Second", DateTimeOffset.UtcNow.AddMinutes(-1));
        var third = CreateMessage(1, 103, "Third", DateTimeOffset.UtcNow);

        // Act
        await postOffice.SyncInboxAsync([refreshedFirst, refreshedSecond, third], TestContext.Current.CancellationToken);

        // Assert
        Assert.Collection(
            postOffice.Inbox.OrderByDescending(x => x.DateReceived),
            message => Assert.Equal(third.MessageId, message.MessageId),
            message => Assert.Equal(second.MessageId, message.MessageId));

        Assert.Single(postOffice.Outbox);
        Assert.Equal(first.MessageId, postOffice.Outbox.Single().MessageHeader.MessageId);
    }

    [Fact]
    public async Task QueueDeliveryAsync_MovesMessageFromInboxToOutbox()
    {
        // Arrange
        var postOffice = new PostOffice();
        var message = CreateMessage(1, 201, "Queued", DateTimeOffset.UtcNow);
        var destination = CreateMailboxId(7);

        await postOffice.AddMessageAsync(message, TestContext.Current.CancellationToken);

        // Act
        await postOffice.QueueDeliveryAsync(message.MessageId, destination, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(postOffice.Inbox);

        var envelope = Assert.Single(postOffice.Outbox);
        Assert.Equal(PostOffice.PendingAction.Delivery, envelope.Action);
        Assert.Equal(destination, envelope.Destination);
        Assert.Equal(message.MessageId, envelope.MessageHeader.MessageId);
    }

    [Fact]
    public async Task CancelAsync_ReturnsQueuedMessageToInbox()
    {
        // Arrange
        var postOffice = new PostOffice();
        var message = CreateMessage(1, 301, "Cancel me", DateTimeOffset.UtcNow);

        await postOffice.AddMessageAsync(message, TestContext.Current.CancellationToken);
        await postOffice.QueueJunkAsync(message, TestContext.Current.CancellationToken);

        // Act
        await postOffice.CancelAsync(message.MessageId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(postOffice.Outbox);
        Assert.Equal(message.MessageId, Assert.Single(postOffice.Inbox).MessageId);
    }

    [Fact]
    public async Task ExecuteNowAsync_MarksEnvelopeAsDueImmediately()
    {
        // Arrange
        var postOffice = new PostOffice { UndoWindowSeconds = 300 };
        var message = CreateMessage(1, 401, "Execute now", DateTimeOffset.UtcNow);

        await postOffice.AddMessageAsync(message, TestContext.Current.CancellationToken);
        await postOffice.QueueJunkAsync(message, TestContext.Current.CancellationToken);

        // Act
        await postOffice.ExecuteNowAsync(message.MessageId, TestContext.Current.CancellationToken);

        // Assert
        var due = postOffice.GetDueEnvelopes(DateTimeOffset.UtcNow);
        Assert.Single(due);
        Assert.Equal(message.MessageId, due.Single().MessageHeader.MessageId);
    }

    [Fact]
    public async Task MarkCompletedAsync_RemovesEnvelopeWithoutReturningMessageToInbox()
    {
        // Arrange
        var postOffice = new PostOffice();
        var message = CreateMessage(1, 501, "Complete me", DateTimeOffset.UtcNow);

        await postOffice.AddMessageAsync(message, TestContext.Current.CancellationToken);
        await postOffice.QueueDeliveryAsync(message.MessageId, CreateMailboxId(11), TestContext.Current.CancellationToken);

        // Act
        await postOffice.MarkCompletedAsync(message.MessageId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(postOffice.Outbox);
        Assert.Empty(postOffice.Inbox);
    }

    [Fact]
    public async Task ReturnToInboxAsync_RestoresQueuedMessage()
    {
        // Arrange
        var postOffice = new PostOffice();
        var message = CreateMessage(1, 601, "Retry me", DateTimeOffset.UtcNow);

        await postOffice.AddMessageAsync(message, TestContext.Current.CancellationToken);
        await postOffice.QueueDeliveryAsync(message.MessageId, CreateMailboxId(12), TestContext.Current.CancellationToken);

        // Act
        await postOffice.ReturnToInboxAsync(message.MessageId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(postOffice.Outbox);
        Assert.Equal(message.MessageId, Assert.Single(postOffice.Inbox).MessageId);
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
}
