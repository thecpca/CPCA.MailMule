using CPCA.MailMule;
using Microsoft.Extensions.Logging;

namespace CPCA.MailMule.Tests;

public class WebTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CPCA_MailMule_AppHost>(cancellationToken);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            // Override the logging filters from the app's configuration
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
            // To output logs to the xUnit.net ITestOutputHelper, consider adding a package from https://www.nuget.org/packages?q=xunit+logging
        });

        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        // Act
        var httpClient = app.CreateHttpClient(MailMuleEndpoints.Frontend);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(MailMuleEndpoints.Frontend, cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        var response = await httpClient.GetAsync("/", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpointsReturnOkForBackendAndImapService()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CPCA_MailMule_AppHost>(cancellationToken);
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        await app.ResourceNotifications.WaitForResourceHealthyAsync(MailMuleEndpoints.Backend, cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(MailMuleEndpoints.ImapService, cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        var backendClient = app.CreateHttpClient(MailMuleEndpoints.Backend);
        var imapClient = app.CreateHttpClient(MailMuleEndpoints.ImapService);

        // Act
        var backendLive = await backendClient.GetAsync("/health/live", cancellationToken);
        var backendReady = await backendClient.GetAsync("/health/ready", cancellationToken);
        var imapLive = await imapClient.GetAsync("/health/live", cancellationToken);
        var imapReady = await imapClient.GetAsync("/health/ready", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, backendLive.StatusCode);
        Assert.Equal(HttpStatusCode.OK, imapLive.StatusCode);

        Assert.True(
            backendReady.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Unexpected backend readiness status code: {backendReady.StatusCode}");

        Assert.True(
            imapReady.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Unexpected IMAP readiness status code: {imapReady.StatusCode}");

        AssertHasCorrelationId(backendLive);
        AssertHasCorrelationId(backendReady);
        AssertHasCorrelationId(imapLive);
        AssertHasCorrelationId(imapReady);
    }

    private static void AssertHasCorrelationId(HttpResponseMessage response)
    {
        Assert.True(
            response.Headers.TryGetValues(CorrelationIdHeaderNames.CorrelationId, out var values),
            $"Response did not include {CorrelationIdHeaderNames.CorrelationId} header.");

        Assert.Contains(values, static value => !String.IsNullOrWhiteSpace(value));
    }
}
