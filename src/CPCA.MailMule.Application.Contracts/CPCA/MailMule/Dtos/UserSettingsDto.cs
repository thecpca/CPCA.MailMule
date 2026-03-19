namespace CPCA.MailMule.Dtos;

public record UserSettingsDto(
    Int32 UndoWindowSeconds,
    Int32 PageSize
);
