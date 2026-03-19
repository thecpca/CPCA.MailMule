using CPCA.MailMule.Dtos;
using CPCA.MailMule.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CPCA.MailMule.Tests;

public sealed class MailboxConfigServiceConnectionTests
{
    [Fact]
    public async Task CreateMailboxAsync_IncomingMailboxWithoutJunkFolder_Throws()
    {
        // Arrange
        var service = BuildServiceProvider(new SpyImapConnectionTester()).GetRequiredService<IMailboxConfigService>();

        var request = new CreateMailboxConfigDto(
            DisplayName: "Incoming A",
            ImapHost: "imap.example.com",
            ImapPort: 993,
            MailboxType: "Incoming",
            Security: "Ssl",
            Username: "user@example.com",
            Password: "secret",
            InboxFolderPath: "INBOX",
            OutboxFolderPath: null,
            SentFolderPath: null,
            ArchiveFolderPath: "Archive",
            JunkFolderPath: null,
            PollIntervalSeconds: 300,
            DeleteMessage: false,
            IsActive: true,
            SortOrder: 0);

        // Act / Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateMailboxAsync(request, TestContext.Current.CancellationToken));
        Assert.Equal("Incoming mailboxes must configure a Junk folder path.", ex.Message);
    }

    [Fact]
    public async Task CreateMailboxAsync_IncomingMailboxWithoutDeleteAndWithArchiveAndJunkFolders_Succeeds()
    {
        // Arrange
        var service = BuildServiceProvider(new SpyImapConnectionTester()).GetRequiredService<IMailboxConfigService>();

        var request = new CreateMailboxConfigDto(
            DisplayName: "Incoming B",
            ImapHost: "imap.example.com",
            ImapPort: 993,
            MailboxType: "Incoming",
            Security: "Ssl",
            Username: "user@example.com",
            Password: "secret",
            InboxFolderPath: "INBOX",
            OutboxFolderPath: null,
            SentFolderPath: null,
            ArchiveFolderPath: "Archive",
            JunkFolderPath: "Junk",
            PollIntervalSeconds: 300,
            DeleteMessage: false,
            IsActive: true,
            SortOrder: 0);

        // Act
        var id = await service.CreateMailboxAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(id > 0);
    }

    [Fact]
    public async Task CreateMailboxAsync_IncomingMailboxWithoutDeleteAndWithoutArchiveFolder_Throws()
    {
        // Arrange
        var service = BuildServiceProvider(new SpyImapConnectionTester()).GetRequiredService<IMailboxConfigService>();

        var request = new CreateMailboxConfigDto(
            DisplayName: "Incoming C",
            ImapHost: "imap.example.com",
            ImapPort: 993,
            MailboxType: "Incoming",
            Security: "Ssl",
            Username: "user@example.com",
            Password: "secret",
            InboxFolderPath: "INBOX",
            OutboxFolderPath: null,
            SentFolderPath: null,
            ArchiveFolderPath: null,
            JunkFolderPath: "Junk",
            PollIntervalSeconds: 300,
            DeleteMessage: false,
            IsActive: true,
            SortOrder: 0);

        // Act / Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateMailboxAsync(request, TestContext.Current.CancellationToken));
        Assert.Equal("Incoming mailboxes that retain messages must configure an Archive folder path.", ex.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidHost_ReturnsValidationErrorWithoutCallingImapTester()
    {
        // Arrange
        var spy = new SpyImapConnectionTester();
        var service = BuildServiceProvider(spy).GetRequiredService<IMailboxConfigService>();

        var request = new MailboxConnectionTestRequest(
            ImapHost: "",
            ImapPort: 993,
            Security: "Auto",
            Username: "user@example.com",
            Password: "secret");

        // Act
        var result = await service.TestConnectionAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("IMAP host is required", result.Message);
        Assert.Equal(0, spy.CallCount);
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidRequest_DelegatesToImapTester()
    {
        // Arrange
        var expected = new MailboxConnectionTestResult(
            Success: true,
            Message: "Connection successful",
            FolderList: new[] { "INBOX", "Archive" });

        var spy = new SpyImapConnectionTester(expected);
        var service = BuildServiceProvider(spy).GetRequiredService<IMailboxConfigService>();

        var request = new MailboxConnectionTestRequest(
            ImapHost: "imap.example.com",
            ImapPort: 993,
            Security: "SSL",
            Username: "user@example.com",
            Password: "secret");

        // Act
        var result = await service.TestConnectionAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expected.Message, result.Message);
        Assert.Equal(expected.FolderList, result.FolderList);
        Assert.Equal(1, spy.CallCount);
    }

    private static ServiceProvider BuildServiceProvider(IImapConnectionTester tester)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMailMule(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddMailMuleApplication();

        // Override the concrete connection tester with a test double.
        services.AddScoped(_ => tester);

        return services.BuildServiceProvider();
    }

    private sealed class SpyImapConnectionTester : IImapConnectionTester
    {
        private readonly MailboxConnectionTestResult result;

        public SpyImapConnectionTester()
            : this(new MailboxConnectionTestResult(false, "Default", null))
        {
        }

        public SpyImapConnectionTester(MailboxConnectionTestResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public Task<MailboxConnectionTestResult> TestConnectionAsync(MailboxConnectionTestRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}
