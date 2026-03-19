using CPCA.MailMule.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CPCA.MailMule.Tests;

public sealed class IncomingMessageRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsOperationalStateWithoutMessageMetadata()
    {
        // Arrange
        await using var serviceProvider = BuildServiceProvider();
        var repository = serviceProvider.GetRequiredService<IncomingMessageRepository>();

        var incomingMessage = new IncomingMessage
        {
            MailboxConfigId = 17,
            Uid = 1001,
            UidValidity = 999,
            State = IncomingMessageState.New,
            DestinationMailboxConfigId = null,
            DiscoveredUtc = DateTimeOffset.UtcNow,
            LastSeenUtc = DateTimeOffset.UtcNow,
            ErrorCode = null,
            ErrorDetail = null,
        };

        // Act
        await repository.AddAsync(incomingMessage, TestContext.Current.CancellationToken);
        var stored = await repository.GetByMailboxAndUidAsync(17, 1001, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(stored);
        Assert.Equal(IncomingMessageState.New, stored.State);
        Assert.Equal((UInt32)999, stored.UidValidity);
        Assert.Null(stored.ErrorCode);
        Assert.Null(stored.ErrorDetail);
    }

    [Fact]
    public async Task GetByMailboxConfigIdAsync_ReturnsMessagesOrderedByUid()
    {
        // Arrange
        await using var serviceProvider = BuildServiceProvider();
        var repository = serviceProvider.GetRequiredService<IncomingMessageRepository>();

        await repository.AddRangeAsync(
        [
            new IncomingMessage { MailboxConfigId = 22, Uid = 300, UidValidity = 1, State = IncomingMessageState.Error, DiscoveredUtc = DateTimeOffset.UtcNow, LastSeenUtc = DateTimeOffset.UtcNow },
            new IncomingMessage { MailboxConfigId = 22, Uid = 100, UidValidity = 1, State = IncomingMessageState.New, DiscoveredUtc = DateTimeOffset.UtcNow, LastSeenUtc = DateTimeOffset.UtcNow },
            new IncomingMessage { MailboxConfigId = 22, Uid = 200, UidValidity = 1, State = IncomingMessageState.Routing, DiscoveredUtc = DateTimeOffset.UtcNow, LastSeenUtc = DateTimeOffset.UtcNow },
        ], TestContext.Current.CancellationToken);

        // Act
        var messages = await repository.GetByMailboxConfigIdAsync(22, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new UInt32[] { 100, 200, 300 }, messages.Select(x => x.Uid).ToArray());
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMailMule(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddMailMuleApplication();

        return services.BuildServiceProvider();
    }
}