namespace CPCA.MailMule;

public sealed class ApplicationSettings
{
    public Int32 Id { get; set; } = 1;

    public Int32 InactivityTimeoutMinutes { get; set; } = 30;
}
