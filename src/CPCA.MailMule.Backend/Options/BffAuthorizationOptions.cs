namespace CPCA.MailMule.Backend.Options;

public sealed class BffAuthorizationOptions
{
    public String[] Administrators { get; set; } = [];

    public String[] Operators { get; set; } = [];
}
