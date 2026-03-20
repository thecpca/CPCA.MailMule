namespace CPCA.MailMule;

public sealed class ActiveSession
{
    public Int32 Id { get; set; } = 1;

    public required String UserId { get; set; }

    public required String UserName { get; set; }

    public DateTimeOffset SessionStartedUtc { get; set; }

    public DateTimeOffset LastActivityUtc { get; set; }
}
