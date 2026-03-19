using System.Net.Http.Json;
using CPCA.MailMule.Dtos;

namespace CPCA.MailMule.Frontend.Services;

public sealed class MailboxConfigApiClient : IMailboxConfigApiClient
{
    private readonly HttpClient httpClient;

    public MailboxConfigApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Get all mailboxes of a specific type (Incoming or Outgoing)
    /// </summary>
    public async Task<IEnumerable<MailboxConfigDto>?> GetMailboxesByTypeAsync(String mailboxType, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.GetAsync($"/admin/{mailboxType.ToLowerInvariant()}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<MailboxConfigDto>>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Get a specific mailbox by ID
    /// </summary>
    public async Task<MailboxConfigDto?> GetMailboxAsync(Int64 id, String mailboxType, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.GetAsync($"/admin/{mailboxType.ToLowerInvariant()}/{id}", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MailboxConfigDto>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Create a new mailbox configuration
    /// </summary>
    public async Task<Boolean> CreateMailboxAsync(CreateMailboxConfigDto dto, String mailboxType, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.PostAsJsonAsync($"/admin/{mailboxType.ToLowerInvariant()}", dto, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Update an existing mailbox configuration
    /// </summary>
    public async Task<Boolean> UpdateMailboxAsync(UpdateMailboxConfigDto dto, String mailboxType, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.PutAsJsonAsync($"/admin/{mailboxType.ToLowerInvariant()}/{dto.Id}", dto, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Delete a mailbox configuration
    /// </summary>
    public async Task<Boolean> DeleteMailboxAsync(Int64 id, String mailboxType, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.DeleteAsync($"/admin/{mailboxType.ToLowerInvariant()}/{id}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Test connection to a mailbox IMAP server
    /// </summary>
    public async Task<MailboxConnectionTestResult?> TestConnectionAsync(MailboxConnectionTestRequest request, String mailboxType, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.PostAsJsonAsync($"/admin/{mailboxType.ToLowerInvariant()}/test-connection", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MailboxConnectionTestResult>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
