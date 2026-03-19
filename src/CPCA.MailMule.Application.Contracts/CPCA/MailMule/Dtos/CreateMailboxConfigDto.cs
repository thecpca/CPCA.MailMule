namespace CPCA.MailMule.Dtos;

public record CreateMailboxConfigDto(
    String DisplayName,
    String ImapHost,
    Int32 ImapPort,
    String MailboxType,
    String Security,
    String Username,
    String Password,
    String? InboxFolderPath,
    String? OutboxFolderPath,
    String? SentFolderPath,
    String? TrashFolderPath,
    Int32 PollIntervalSeconds,
    Boolean DeleteMessage,
    Boolean IsActive,
    Int32 SortOrder
);
