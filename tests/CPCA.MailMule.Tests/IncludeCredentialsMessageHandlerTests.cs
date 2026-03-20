using CPCA.MailMule.Frontend.Services;

namespace CPCA.MailMule.Tests;

public sealed class IncludeCredentialsMessageHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsAjaxAndJsonHeaders()
    {
        var innerHandler = new CaptureHandler();
        var handler = new IncludeCredentialsMessageHandler
        {
            InnerHandler = innerHandler
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5001")
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/settings");
        _ = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(innerHandler.CapturedRequest);
        Assert.True(innerHandler.CapturedRequest!.Headers.Contains("X-Requested-With"));
        Assert.Contains(
            innerHandler.CapturedRequest.Headers.GetValues("X-Requested-With"),
            value => String.Equals(value, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            innerHandler.CapturedRequest.Headers.Accept,
            header => String.Equals(header.MediaType, "application/json", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request
            });
        }
    }
}
