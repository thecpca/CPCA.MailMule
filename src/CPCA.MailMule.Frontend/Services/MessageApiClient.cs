using System.Net.Http.Json;

namespace CPCA.MailMule.Frontend.Services;

public sealed class MessageApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<MessageHeader>> GetHeadersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<MessageHeader>>("/api/messages", cancellationToken)
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<MessageBodyDto?> GetMessageAsync(MessageId messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<MessageBodyDto>(
                $"/api/messages/{messageId.Mailbox.Value}/{messageId.Uid}",
                cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Boolean> RouteToJunkAsync(MessageId messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsync($"/api/messages/{messageId.Mailbox.Value}/{messageId.Uid}/junk", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Boolean> RouteToMailboxAsync(MessageId messageId, MailboxId destinationMailboxId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsync(
                $"/api/messages/{messageId.Mailbox.Value}/{messageId.Uid}/route/{destinationMailboxId.Value}",
                null,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
