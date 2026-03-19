namespace CPCA.MailMule.Services;

using CPCA.MailMule.Dtos;

public interface IUserSettingsService
{
    Task<UserSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(UserSettingsDto settings, CancellationToken cancellationToken = default);
}
