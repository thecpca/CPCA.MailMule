namespace CPCA.MailMule.Services;

using CPCA.MailMule.Dtos;
using CPCA.MailMule.Repositories;
using Microsoft.Extensions.Logging;

internal sealed class IncomingMessageService : IIncomingMessageService
{
    private readonly IncomingMessageRepository repository;
    private readonly ILogger<IncomingMessageService> logger;

    public IncomingMessageService(IncomingMessageRepository repository, ILogger<IncomingMessageService> logger)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<IncomingMessageDto>> GetErrorMessagesAsync(CancellationToken cancellationToken = default)
    {
        var messages = await this.repository.ToListAsync(
            x => x.State == IncomingMessageState.Error,
            cancellationToken);

        return messages
            .OrderByDescending(x => x.ErrorUtc ?? x.DiscoveredUtc)
            .Select(ToDto)
            .ToList();
    }

    public async Task RequeueAsync(Int64 id, CancellationToken cancellationToken = default)
    {
        var message = await this.repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"IncomingMessage with ID {id} was not found.");

        if (message.State != IncomingMessageState.Error)
        {
            throw new InvalidOperationException($"IncomingMessage {id} is in state {message.State}, not Error. Only Error messages can be requeued.");
        }

        message.State = IncomingMessageState.New;
        message.StateChangedUtc = DateTimeOffset.UtcNow;
        message.ErrorUtc = null;
        message.ErrorCode = null;
        message.ErrorDetail = null;

        this.repository.Update(message);
        await this.repository.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation(
            "IncomingMessage {MessageId} (UID {Uid}, Mailbox {MailboxConfigId}) requeued from Error to New",
            id, message.Uid, message.MailboxConfigId);
    }

    public async Task DismissAsync(Int64 id, CancellationToken cancellationToken = default)
    {
        var message = await this.repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"IncomingMessage with ID {id} was not found.");

        if (message.State != IncomingMessageState.Error)
        {
            throw new InvalidOperationException($"IncomingMessage {id} is in state {message.State}, not Error. Only Error messages can be dismissed.");
        }

        this.repository.Remove(message);
        await this.repository.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation(
            "IncomingMessage {MessageId} (UID {Uid}, Mailbox {MailboxConfigId}) dismissed and removed",
            id, message.Uid, message.MailboxConfigId);
    }

    private static IncomingMessageDto ToDto(IncomingMessage entity)
    {
        return new IncomingMessageDto(
            Id: entity.Id,
            MailboxConfigId: entity.MailboxConfigId,
            Uid: entity.Uid,
            UidValidity: entity.UidValidity,
            State: entity.State.ToString(),
            DestinationMailboxConfigId: entity.DestinationMailboxConfigId,
            DiscoveredUtc: entity.DiscoveredUtc,
            LastSeenUtc: entity.LastSeenUtc,
            StateChangedUtc: entity.StateChangedUtc,
            ErrorUtc: entity.ErrorUtc,
            ErrorCode: entity.ErrorCode,
            ErrorDetail: entity.ErrorDetail);
    }
}
