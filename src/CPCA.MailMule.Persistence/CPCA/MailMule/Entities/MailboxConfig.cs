namespace CPCA.MailMule;

public sealed class MailboxConfig
{
    public Int64 Id { get; set; }

    public MailboxType MailboxType { get; set; } = MailboxType.Undefined;

    public String DisplayName { get; set; } = String.Empty;

    public String ImapHost { get; set; } = String.Empty;

    public Int32 ImapPort { get; set; } = 993;

    public MailboxSecurity Security { get; set; } = MailboxSecurity.Auto;

    public String Username { get; set; } = String.Empty;

    public String EncryptedPassword { get; set; } = String.Empty;

    public String? InboxFolderPath { get; set; }

    public String? OutboxFolderPath { get; set; }

    public String? SentFolderPath { get; set; }

    public String? TrashFolderPath { get; set; }

    public Int32 PollIntervalSeconds { get; set; } = 20;

    public Boolean DeleteMessage { get; set; } = false;

    public Boolean IsActive { get; set; } = true;

    public DateTime? LastPolledUtc { get; set; }

    public Int32 SortOrder { get; set; }
}
