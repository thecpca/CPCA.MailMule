namespace CPCA.MailMule.Dtos;

public record IncomingMessageDto(
    Int64 Id,
    Int64 MailboxConfigId,
    UInt32 Uid,
    UInt32 UidValidity,
    String State,
    Int64? DestinationMailboxConfigId,
    DateTimeOffset DiscoveredUtc,
    DateTimeOffset LastSeenUtc,
    DateTimeOffset? StateChangedUtc,
    DateTimeOffset? ErrorUtc,
    String? ErrorCode,
    String? ErrorDetail
);
