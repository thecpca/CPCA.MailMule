using System.Net.Http.Json;
using CPCA.MailMule.Dtos;

namespace CPCA.MailMule.Frontend.Services;

public sealed class ApplicationSettingsApiClient(HttpClient httpClient) : IApplicationSettingsApiClient
{
    public async Task<ApplicationSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("/admin/app-settings", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ApplicationSettingsDto>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<Boolean> UpdateAsync(ApplicationSettingsDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync("/admin/app-settings", dto, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
