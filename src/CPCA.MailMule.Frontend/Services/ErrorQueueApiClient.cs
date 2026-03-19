using System.Net.Http.Json;
using CPCA.MailMule.Dtos;

namespace CPCA.MailMule.Frontend.Services;

public sealed class ErrorQueueApiClient(HttpClient httpClient) : IErrorQueueApiClient
{
    public async Task<IReadOnlyList<IncomingMessageDto>> GetErrorsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("/admin/errors", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<IReadOnlyList<IncomingMessageDto>>(cancellationToken) ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public async Task<Boolean> RequeueAsync(Int64 id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsync($"/admin/errors/{id}/requeue", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<Boolean> DismissAsync(Int64 id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsync($"/admin/errors/{id}/dismiss", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
