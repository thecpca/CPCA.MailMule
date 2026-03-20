using System.Net.Http.Json;

namespace CPCA.MailMule.Frontend.Services;

public sealed class SessionApiClient : ISessionApiClient
{
    private readonly HttpClient httpClient;

    public SessionApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<SessionStatusDto?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.GetAsync("/session/status", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SessionStatusDto>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<SessionClaimResultDto?> ClaimAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.PostAsync("/session/claim", null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SessionClaimResultDto>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<Boolean> HeartbeatAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.PostAsync("/session/heartbeat", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<Boolean> ReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.PostAsync("/session/release", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
