using CPCA.MailMule.Dtos;

namespace CPCA.MailMule.Frontend.Services;

public interface IUserSettingsApiClient
{
    Task<UserSettingsDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<Boolean> UpdateAsync(UserSettingsDto dto, CancellationToken cancellationToken = default);
}
