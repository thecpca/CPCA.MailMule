using System.Net.Http.Json;
using CPCA.MailMule.Dtos;

namespace CPCA.MailMule.Frontend.Services;

public sealed class UserSettingsApiClient : IUserSettingsApiClient
{
    private readonly HttpClient httpClient;

    public UserSettingsApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<UserSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.GetAsync("/admin/settings", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserSettingsDto>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<Boolean> UpdateAsync(UserSettingsDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.PutAsJsonAsync("/admin/settings", dto, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
