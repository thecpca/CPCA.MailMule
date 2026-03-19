using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CPCA.MailMule;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMailMule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDb)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDb);

        services.AddDataProtection();
        services.AddSingleton<IStringProtector, DataProtectionStringProtector>();
        services.AddDbContext<MailMuleDbContext>(configureDb);

        return services;
    }
}
