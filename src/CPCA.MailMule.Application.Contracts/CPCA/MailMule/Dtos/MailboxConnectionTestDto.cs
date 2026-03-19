namespace CPCA.MailMule.Dtos;

public record MailboxConnectionTestRequest(
    String ImapHost,
    Int32 ImapPort,
    String Security,
    String Username,
    String Password
);

public record MailboxConnectionTestResult(
    Boolean Success,
    String? Message,
    String[]? FolderList
);
