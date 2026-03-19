namespace CPCA.MailMule;

public sealed class UserSettings
{
    public Int32 Id { get; set; } = 1;

    public Int32 UndoWindowSeconds { get; set; } = 15;

    public Int32 PageSize { get; set; } = 25;
}
