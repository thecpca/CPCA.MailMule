using CPCA.MailMule;

namespace CPCA.MailMule.Tests;

public sealed class BackendControllerSecurityTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task BffUserReturnsUnauthorizedForAnonymousAjaxRequest()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CPCA_MailMule_AppHost>(cancellationToken);
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(MailMuleEndpoints.Backend, cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        var backendClient = app.CreateHttpClient(MailMuleEndpoints.Backend);
        using var request = CreateAjaxRequest(HttpMethod.Get, "/bff/user");

        // Act
        using var response = await backendClient.SendAsync(request, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/session/MessageRouting/status")]
    [InlineData("/session/MessageRouting/claim")]
    [InlineData("/session/MessageRouting/heartbeat")]
    [InlineData("/session/MessageRouting/release")]
    public async Task SessionEndpointsReturnUnauthorizedForAnonymousAjaxRequest(String path)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CPCA_MailMule_AppHost>(cancellationToken);
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(MailMuleEndpoints.Backend, cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        var backendClient = app.CreateHttpClient(MailMuleEndpoints.Backend);
        var method = path.EndsWith("/status", StringComparison.OrdinalIgnoreCase)
            ? HttpMethod.Get
            : HttpMethod.Post;
        using var request = CreateAjaxRequest(method, path);

        // Act
        using var response = await backendClient.SendAsync(request, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/admin/incoming")]
    [InlineData("/admin/outgoing")]
    [InlineData("/admin/settings")]
    [InlineData("/admin/app-settings")]
    [InlineData("/admin/errors")]
    public async Task AdminEndpointsDenyAnonymousAjaxRequest(String path)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CPCA_MailMule_AppHost>(cancellationToken);
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(MailMuleEndpoints.Backend, cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        var backendClient = app.CreateHttpClient(MailMuleEndpoints.Backend);
        using var request = CreateAjaxRequest(HttpMethod.Get, path);

        // Act
        using var response = await backendClient.SendAsync(request, cancellationToken);

        // Assert
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest,
            $"Expected unauthorized/bad request for {path}, got {response.StatusCode}.");
    }

    private static HttpRequestMessage CreateAjaxRequest(HttpMethod method, String path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return request;
    }
}
