using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CPCA.MailMule.Frontend.Services;

public sealed class IncludeCredentialsMessageHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}