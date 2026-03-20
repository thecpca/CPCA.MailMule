namespace CPCA.MailMule;

using Microsoft.Extensions.DependencyInjection;
using CPCA.MailMule.Repositories;
using CPCA.MailMule.Services;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddMailMuleApplication(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<MailboxConfigRepository>();
        services.AddScoped<IncomingMessageRepository>();

        // Register application services
        services.AddScoped<IImapConnectionTester, MailKitConnectionTester>();
        services.AddScoped<IImapClientFactory, MailKitImapClientFactory>();
        services.AddScoped<IMailboxService, MailKitMailboxService>();
        services.AddScoped<IMailboxConfigService, MailboxConfigService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<IApplicationSettingsService, ApplicationSettingsService>();
        services.AddScoped<IIncomingMessageService, IncomingMessageService>();
        services.AddScoped<IKingOfTheHillService, KingOfTheHillService>();

        return services;
    }
}
