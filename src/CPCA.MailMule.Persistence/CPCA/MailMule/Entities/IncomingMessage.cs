namespace CPCA.MailMule;

public sealed class IncomingMessage
{
    public Int64 Id { get; set; }

    public Int64 MailboxConfigId { get; set; }

    public UInt32 Uid { get; set; }

    public UInt32 UidValidity { get; set; }

    public IncomingMessageState State { get; set; } = IncomingMessageState.New;

    public Int64? DestinationMailboxConfigId { get; set; }

    public DateTimeOffset DiscoveredUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StateChangedUtc { get; set; }

    public DateTimeOffset? ErrorUtc { get; set; }

    public String? ErrorCode { get; set; }

    public String? ErrorDetail { get; set; }
}