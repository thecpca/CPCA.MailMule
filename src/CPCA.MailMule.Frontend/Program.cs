using CPCA.MailMule.Frontend.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CPCA.MailMule.Frontend;

public static class Program
{
    public static async Task Main(String[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .Enrich.WithProperty("InstanceId", Guid.NewGuid().ToString("n"))
            .Enrich.WithProperty("ApplicationName", ThisAssembly.AssemblyTitle)
            .Enrich.WithProperty("ApplicationVersion", ThisAssembly.AssemblyVersion)
            .ReadFrom.Configuration(builder.Configuration)
            .WriteTo.BrowserConsole()
            .CreateLogger();

        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddMudServices();

        // HttpClient points to the BFF; address is read from wwwroot/appsettings.Development.json
        var bffBaseUrl = builder.Configuration["Bff:BaseUrl"]
            ?? throw new InvalidOperationException("Bff:BaseUrl is not configured.");

        builder.Services.AddScoped(_ => new HttpClient(
            new IncludeCredentialsMessageHandler
            {
                InnerHandler = new HttpClientHandler()
            })
        {
            BaseAddress = new Uri(bffBaseUrl)
        });

        // Auth state comes from BFF, not OIDC
        builder.Services.AddScoped<AuthenticationStateProvider, BffAuthenticationStateProvider>();
        builder.Services.AddAuthorizationCore();
        // Register API clients
        builder.Services.AddScoped<MailboxConfigApiClient>();
        builder.Services.AddScoped<MessageApiClient>();
        builder.Services.AddScoped<IMailboxConfigApiClient>(sp => sp.GetRequiredService<MailboxConfigApiClient>());
        builder.Services.AddScoped<IMessageApiClient>(sp => sp.GetRequiredService<MessageApiClient>());
        builder.Services.AddScoped<UserSettingsApiClient>();
        builder.Services.AddScoped<IUserSettingsApiClient>(sp => sp.GetRequiredService<UserSettingsApiClient>());
        builder.Services.AddScoped<ApplicationSettingsApiClient>();
        builder.Services.AddScoped<IApplicationSettingsApiClient>(sp => sp.GetRequiredService<ApplicationSettingsApiClient>());
        builder.Services.AddScoped<ErrorQueueApiClient>();
        builder.Services.AddScoped<IErrorQueueApiClient>(sp => sp.GetRequiredService<ErrorQueueApiClient>());
        builder.Services.AddScoped<SessionApiClient>();
        builder.Services.AddScoped<ISessionApiClient>(sp => sp.GetRequiredService<SessionApiClient>());
        builder.Services.AddScoped<PostOffice>();
        builder.Services.AddScoped<PostOfficeWorker>();


        var app = builder.Build();

        await app.RunAsync();
    }
}
