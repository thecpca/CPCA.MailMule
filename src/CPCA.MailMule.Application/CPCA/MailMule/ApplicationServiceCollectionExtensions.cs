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

        // Register application services
        services.AddScoped<IMailboxConfigService, MailboxConfigService>();

        return services;
    }
}
