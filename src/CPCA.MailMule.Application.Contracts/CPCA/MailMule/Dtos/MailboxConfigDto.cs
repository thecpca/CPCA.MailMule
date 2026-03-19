namespace CPCA.MailMule.Dtos;

public record MailboxConfigDto(
    Int64 Id,
    String DisplayName,
    String ImapHost,
    Int32 ImapPort,
    String MailboxType,
    String Security,
    String Username,
    String? InboxFolderPath,
    String? OutboxFolderPath,
    String? SentFolderPath,
    String? ArchiveFolderPath,
    String? JunkFolderPath,
    Int32 PollIntervalSeconds,
    Boolean DeleteMessage,
    Boolean IsActive,
    DateTime? LastPolledUtc,
    Int32 SortOrder
);
