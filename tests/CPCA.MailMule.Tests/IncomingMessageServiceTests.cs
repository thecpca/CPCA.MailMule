using CPCA.MailMule.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CPCA.MailMule.Tests;

public sealed class IncomingMessageServiceTests
{
    [Fact]
    public async Task GetErrorMessagesAsync_ReturnsOnlyErrorState()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderWithErrorMessagesAsync();
        var service = serviceProvider.GetRequiredService<IIncomingMessageService>();

        // Act
        var errors = await service.GetErrorMessagesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, errors.Count);
        Assert.All(errors, e => Assert.Equal("Error", e.State));
    }

    [Fact]
    public async Task GetErrorMessagesAsync_OrdersByErrorUtcDescending()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderWithErrorMessagesAsync();
        var service = serviceProvider.GetRequiredService<IIncomingMessageService>();

        // Act
        var errors = await service.GetErrorMessagesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(errors.Count >= 2);
        Assert.True(errors[0].ErrorUtc >= errors[1].ErrorUtc);
    }

    [Fact]
    public async Task RequeueAsync_SetsStateToNewAndClearsErrorFields()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderWithErrorMessagesAsync();
        var service = serviceProvider.GetRequiredService<IIncomingMessageService>();

        var errors = await service.GetErrorMessagesAsync(TestContext.Current.CancellationToken);
        var targetId = errors[0].Id;

        // Act
        await service.RequeueAsync(targetId, TestContext.Current.CancellationToken);

        // Assert — the requeued message should no longer appear in the error list
        var remaining = await service.GetErrorMessagesAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(remaining, e => e.Id == targetId);

        // Verify it still exists in the database with State=New
        var db = serviceProvider.GetRequiredService<MailMuleDbContext>();
        var entity = await db.IncomingMessages.FindAsync([targetId], TestContext.Current.CancellationToken);
        Assert.NotNull(entity);
        Assert.Equal(IncomingMessageState.New, entity.State);
        Assert.Null(entity.ErrorCode);
        Assert.Null(entity.ErrorDetail);
        Assert.Null(entity.ErrorUtc);
    }

    [Fact]
    public async Task RequeueAsync_ThrowsForNonErrorMessage()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderWithErrorMessagesAsync();
        var service = serviceProvider.GetRequiredService<IIncomingMessageService>();

        var db = serviceProvider.GetRequiredService<MailMuleDbContext>();
        var newMsg = await db.IncomingMessages.FirstAsync(m => m.State == IncomingMessageState.New, TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RequeueAsync(newMsg.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DismissAsync_RemovesMessage()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderWithErrorMessagesAsync();
        var service = serviceProvider.GetRequiredService<IIncomingMessageService>();

        var errors = await service.GetErrorMessagesAsync(TestContext.Current.CancellationToken);
        var targetId = errors[0].Id;

        // Act
        await service.DismissAsync(targetId, TestContext.Current.CancellationToken);

        // Assert
        var remaining = await service.GetErrorMessagesAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(remaining, e => e.Id == targetId);

        var db = serviceProvider.GetRequiredService<MailMuleDbContext>();
        var entity = await db.IncomingMessages.FindAsync([targetId], TestContext.Current.CancellationToken);
        Assert.Null(entity);
    }

    [Fact]
    public async Task DismissAsync_ThrowsForNonErrorMessage()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderWithErrorMessagesAsync();
        var service = serviceProvider.GetRequiredService<IIncomingMessageService>();

        var db = serviceProvider.GetRequiredService<MailMuleDbContext>();
        var newMsg = await db.IncomingMessages.FirstAsync(m => m.State == IncomingMessageState.New, TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DismissAsync(newMsg.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RequeueAsync_ThrowsForNonExistentId()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderWithErrorMessagesAsync();
        var service = serviceProvider.GetRequiredService<IIncomingMessageService>();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.RequeueAsync(99999, TestContext.Current.CancellationToken));
    }

    private static async Task<ServiceProvider> BuildServiceProviderWithErrorMessagesAsync()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMailMule(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddMailMuleApplication();

        var sp = services.BuildServiceProvider();

        var db = sp.GetRequiredService<MailMuleDbContext>();
        var now = DateTimeOffset.UtcNow;

        db.IncomingMessages.AddRange(
            new IncomingMessage
            {
                MailboxConfigId = 1,
                Uid = 100,
                UidValidity = 1,
                State = IncomingMessageState.Error,
                DiscoveredUtc = now.AddMinutes(-10),
                LastSeenUtc = now.AddMinutes(-10),
                ErrorUtc = now.AddMinutes(-5),
                ErrorCode = "IMAP_TIMEOUT",
                ErrorDetail = "Connection timed out",
            },
            new IncomingMessage
            {
                MailboxConfigId = 1,
                Uid = 200,
                UidValidity = 1,
                State = IncomingMessageState.Error,
                DiscoveredUtc = now.AddMinutes(-20),
                LastSeenUtc = now.AddMinutes(-20),
                ErrorUtc = now.AddMinutes(-15),
                ErrorCode = "PARSE_FAILURE",
                ErrorDetail = "Could not parse MIME body",
            },
            new IncomingMessage
            {
                MailboxConfigId = 1,
                Uid = 300,
                UidValidity = 1,
                State = IncomingMessageState.New,
                DiscoveredUtc = now.AddMinutes(-3),
                LastSeenUtc = now.AddMinutes(-3),
            });

        await db.SaveChangesAsync();

        return sp;
    }
}
