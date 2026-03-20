namespace CPCA.MailMule;

public sealed class ActiveSession
{
    public Int32 Id { get; set; }

    public Kingdom Kingdom { get; set; }

    public required String UserId { get; set; }

    public required String UserName { get; set; }

    public DateTimeOffset SessionStartedUtc { get; set; }

    public DateTimeOffset LastActivityUtc { get; set; }
}
