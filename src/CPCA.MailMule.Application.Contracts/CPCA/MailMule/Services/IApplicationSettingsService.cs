namespace CPCA.MailMule.Services;

using CPCA.MailMule.Dtos;

public interface IApplicationSettingsService
{
    Task<ApplicationSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(ApplicationSettingsDto settings, CancellationToken cancellationToken = default);
}
