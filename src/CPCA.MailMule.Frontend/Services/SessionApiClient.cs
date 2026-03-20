using System.Net.Http.Json;

namespace CPCA.MailMule.Frontend.Services;

public sealed class SessionApiClient : ISessionApiClient
{
    private readonly HttpClient httpClient;

    public SessionApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<SessionStatusDto?> GetStatusAsync(Kingdom kingdom, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.GetAsync($"/session/{kingdom}/status", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SessionStatusDto>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<SessionClaimResultDto?> ClaimAsync(Kingdom kingdom, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.PostAsync($"/session/{kingdom}/claim", null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SessionClaimResultDto>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<Boolean> HeartbeatAsync(Kingdom kingdom, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.PostAsync($"/session/{kingdom}/heartbeat", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<Boolean> ReleaseAsync(Kingdom kingdom)
    {
        try
        {
            var response = await this.httpClient.PostAsync($"/session/{kingdom}/release", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
