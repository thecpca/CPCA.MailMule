using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CPCA.MailMule.Backend.HealthChecks;

internal sealed class BackendDatabaseReadyHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<MailMuleDbContext>();
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        if (!canConnect)
        {
            return HealthCheckResult.Unhealthy("Database connection check failed.");
        }

        return HealthCheckResult.Healthy("Database connection is healthy.");
    }
}
