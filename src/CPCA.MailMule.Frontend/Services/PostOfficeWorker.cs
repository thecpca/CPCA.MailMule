using CPCA.MailMule.Dtos;

namespace CPCA.MailMule.Frontend.Services;

public sealed class PostOfficeWorker(
    IMessageApiClient messageApiClient,
    IMailboxConfigApiClient mailboxConfigApiClient,
    IUserSettingsApiClient userSettingsApiClient,
    PostOffice postOffice)
{
    private readonly Lock syncRoot = new();
    private readonly List<MailboxConfigDto> outgoingMailboxes = [];
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private CancellationTokenSource? workerCancellationTokenSource;
    private Task? pendingQueueTask;
    private Task? inboxSyncTask;
    private Int32 inboxPollSeconds = 30;
    private Boolean started;

    public event Action? Changed;
    public event Action<PostOfficeWorkerNotification>? NotificationRaised;

    public IReadOnlyCollection<MailboxConfigDto> OutgoingMailboxes
    {
        get
        {
            lock (syncRoot)
            {
                return outgoingMailboxes.ToArray();
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (started)
        {
            return;
        }

        started = true;
        await RefreshAsync(cancellationToken);

        workerCancellationTokenSource = new CancellationTokenSource();
        pendingQueueTask = RunPendingQueueAsync(workerCancellationTokenSource.Token);
        inboxSyncTask = RunInboxSyncAsync(workerCancellationTokenSource.Token);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            var loadedHeaders = await messageApiClient.GetHeadersAsync(cancellationToken);
            var loadedOutgoing = (await mailboxConfigApiClient.GetMailboxesByTypeAsync("Outgoing", cancellationToken) ?? [])
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToArray();
            var loadedIncoming = (await mailboxConfigApiClient.GetMailboxesByTypeAsync("Incoming", cancellationToken) ?? [])
                .Where(x => x.IsActive)
                .ToArray();

            lock (syncRoot)
            {
                outgoingMailboxes.Clear();
                outgoingMailboxes.AddRange(loadedOutgoing);
                inboxPollSeconds = ResolveInboxPollIntervalSeconds(loadedIncoming);
            }

            var userSettings = await userSettingsApiClient.GetAsync(cancellationToken);
            if (userSettings != null)
            {
                postOffice.UndoWindowSeconds = userSettings.UndoWindowSeconds;
            }

            var syncedHeaders = loadedHeaders.Select(ToDto).ToArray();
            await postOffice.SyncInboxAsync(syncedHeaders, cancellationToken);
            Changed?.Invoke();
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public async Task ProcessDueEnvelopesAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var dueEnvelopes = postOffice.GetDueEnvelopes(now);

        foreach (var pendingEnvelope in dueEnvelopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecutePendingAsync(pendingEnvelope, cancellationToken);
        }
    }

    public String GetMailboxDisplayName(MailboxId? mailboxId)
    {
        if (mailboxId == null)
        {
            return "Unknown mailbox";
        }

        var mailboxKey = ToInt64FromGuid(mailboxId.Value.Value);

        lock (syncRoot)
        {
            return outgoingMailboxes.FirstOrDefault(x => x.Id == mailboxKey)?.DisplayName ?? "Unknown mailbox";
        }
    }

    private async Task RunPendingQueueAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await ProcessDueEnvelopesAsync(DateTimeOffset.UtcNow, cancellationToken);
        }
    }

    private async Task RunInboxSyncAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(GetInboxPollSeconds()), cancellationToken);
                await RefreshAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                NotificationRaised?.Invoke(new PostOfficeWorkerNotification(
                    PostOfficeWorkerNotificationLevel.Error,
                    "Automatic inbox sync failed. Retrying on the next cycle."));
            }
        }
    }

    private async Task ExecutePendingAsync(PostOffice.PendingEnvelope pendingEnvelope, CancellationToken cancellationToken)
    {
        Boolean success;

        if (pendingEnvelope.Action == PostOffice.PendingAction.Junk)
        {
            success = await messageApiClient.RouteToJunkAsync(pendingEnvelope.MessageHeader.MessageId, cancellationToken);
        }
        else if (pendingEnvelope.Destination != null)
        {
            success = await messageApiClient.RouteToMailboxAsync(
                pendingEnvelope.MessageHeader.MessageId,
                pendingEnvelope.Destination.Value,
                cancellationToken);
        }
        else
        {
            success = false;
        }

        if (success)
        {
            await postOffice.MarkCompletedAsync(pendingEnvelope.MessageHeader.MessageId, cancellationToken);
            NotificationRaised?.Invoke(new PostOfficeWorkerNotification(
                PostOfficeWorkerNotificationLevel.Success,
                pendingEnvelope.Action == PostOffice.PendingAction.Junk
                    ? "Message moved to junk."
                    : "Message routed successfully."));
            return;
        }

        await postOffice.ReturnToInboxAsync(pendingEnvelope.MessageHeader.MessageId, cancellationToken);
        NotificationRaised?.Invoke(new PostOfficeWorkerNotification(
            PostOfficeWorkerNotificationLevel.Error,
            pendingEnvelope.Action == PostOffice.PendingAction.Junk
                ? "Failed to move message to junk. Message returned to inbox."
                : "Failed to route message. Message returned to inbox."));
    }

    private Int32 GetInboxPollSeconds()
    {
        lock (syncRoot)
        {
            return inboxPollSeconds;
        }
    }

    private static MessageHeaderDto ToDto(MessageHeader header)
    {
        return new MessageHeaderDto
        {
            MessageId = header.Id,
            From = header.From,
            Subject = header.Subject,
            DateSent = header.Date,
            DateReceived = header.Date,
            To = []
        };
    }

    private static Int32 ResolveInboxPollIntervalSeconds(IEnumerable<MailboxConfigDto> incomingMailboxes)
    {
        var configuredIntervals = incomingMailboxes
            .Select(x => x.PollIntervalSeconds)
            .Where(x => x > 0)
            .ToList();

        if (configuredIntervals.Count == 0)
        {
            return 30;
        }

        return Math.Clamp(configuredIntervals.Min(), 5, 300);
    }

    private static Int64 ToInt64FromGuid(Guid value)
    {
        var bytes = value.ToByteArray();
        return BitConverter.ToInt64(bytes, 0);
    }
}

public readonly record struct PostOfficeWorkerNotification(PostOfficeWorkerNotificationLevel Level, String Message);

public enum PostOfficeWorkerNotificationLevel
{
    Info,
    Success,
    Error,
}