using System.Diagnostics;
using CPCA.MailMule;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace CPCA.MailMule.Tests;

public class WebTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly SemaphoreSlim PlaywrightInstallLock = new(1, 1);
    private static volatile bool _chromiumInstalled;

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

    [Fact]
    public async Task FrontendHomePageRendersInChromium()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CPCA_MailMule_AppHost>(cancellationToken);
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(MailMuleEndpoints.Frontend, cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);

        var frontendClient = app.CreateHttpClient(MailMuleEndpoints.Frontend);
        var frontendUrl = frontendClient.BaseAddress?.ToString()
            ?? throw new InvalidOperationException("Frontend HttpClient did not expose a BaseAddress.");

        await EnsureChromiumInstalledAsync(cancellationToken);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        var response = await page.GotoAsync(frontendUrl, new PageGotoOptions
        {
            Timeout = (float)DefaultTimeout.TotalMilliseconds,
            WaitUntil = WaitUntilState.NetworkIdle
        });

        Assert.NotNull(response);
        Assert.Equal((int)HttpStatusCode.OK, response.Status);
        Assert.Equal("Home", await page.TitleAsync());
        Assert.True(
            await page.GetByText("Hello, world!", new PageGetByTextOptions { Exact = true }).IsVisibleAsync(),
            "Expected the anonymous home-page heading to be visible.");
        Assert.True(
            await page.GetByText("Welcome to your new app, powered by MudBlazor and the .NET 10 Template!", new PageGetByTextOptions { Exact = true }).IsVisibleAsync(),
            "Expected the anonymous home-page welcome text to be visible.");
    }

    private static void AssertHasCorrelationId(HttpResponseMessage response)
    {
        Assert.True(
            response.Headers.TryGetValues(CorrelationIdHeaderNames.CorrelationId, out var values),
            $"Response did not include {CorrelationIdHeaderNames.CorrelationId} header.");

        Assert.Contains(values, static value => !String.IsNullOrWhiteSpace(value));
    }

    private static async Task EnsureChromiumInstalledAsync(CancellationToken cancellationToken)
    {
        if (_chromiumInstalled)
        {
            return;
        }

        await PlaywrightInstallLock.WaitAsync(cancellationToken);

        try
        {
            if (_chromiumInstalled)
            {
                return;
            }

            var installScriptPath = Path.Combine(AppContext.BaseDirectory, "playwright.ps1");
            if (!File.Exists(installScriptPath))
            {
                throw new FileNotFoundException(
                    $"Could not find the Playwright install script at '{installScriptPath}'.",
                    installScriptPath);
            }

            using var installProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pwsh",
                    Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{installScriptPath}\" install chromium",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = AppContext.BaseDirectory
                }
            };

            installProcess.Start();

            var standardOutputTask = installProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = installProcess.StandardError.ReadToEndAsync(cancellationToken);

            await installProcess.WaitForExitAsync(cancellationToken);

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;

            if (installProcess.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Playwright browser installation failed with exit code {installProcess.ExitCode}.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
            }

            _chromiumInstalled = true;
        }
        finally
        {
            PlaywrightInstallLock.Release();
        }
    }
}
