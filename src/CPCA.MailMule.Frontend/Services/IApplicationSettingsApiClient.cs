using CPCA.MailMule.Dtos;

namespace CPCA.MailMule.Frontend.Services;

public interface IApplicationSettingsApiClient
{
    Task<ApplicationSettingsDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<Boolean> UpdateAsync(ApplicationSettingsDto dto, CancellationToken cancellationToken = default);
}
