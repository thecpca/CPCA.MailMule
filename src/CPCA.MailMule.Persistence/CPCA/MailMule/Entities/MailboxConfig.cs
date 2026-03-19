namespace CPCA.MailMule;

public sealed class MailboxConfig
{
    public Guid Id { get; set; }

    public MailboxType MailboxType { get; set; } = MailboxType.Undefined;

    public String DisplayName { get; set; } = String.Empty;

    public String ImapHost { get; set; } = String.Empty;

    public Int32 ImapPort { get; set; } = 993;

    public MailboxSecurity Security { get; set; } = MailboxSecurity.Auto;

    public String Username { get; set; } = String.Empty;

    public String EncryptedPassword { get; set; } = String.Empty;

    public String InboxFolder { get; set; } = "INBOX";

    public String JunkFolder { get; set; } = "Junk E-mail";

    public String ArchiveFolder { get; set; } = "Archive";

    public String ErrorFolder { get; set; } = "Error";

    public Int32 PollIntervalSeconds { get; set; } = 20;

    public Boolean DeleteMessage { get; set; } = false;

    public Boolean IsActive { get; set; } = true;

    public DateTimeOffset? LastPolledUtc { get; set; }

    public Int32 SortOrder { get; set; }
}
