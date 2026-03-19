namespace CPCA.MailMule.Services;

using CPCA.MailMule.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

internal sealed class ApplicationSettingsService : IApplicationSettingsService
{
    private readonly MailMuleDbContext dbContext;
    private readonly ILogger<ApplicationSettingsService> logger;

    public ApplicationSettingsService(MailMuleDbContext dbContext, ILogger<ApplicationSettingsService> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ApplicationSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await this.dbContext.ApplicationSettings.FirstAsync(cancellationToken);
        return new ApplicationSettingsDto(settings.InactivityTimeoutMinutes);
    }

    public async Task UpdateAsync(ApplicationSettingsDto dto, CancellationToken cancellationToken = default)
    {
        var settings = await this.dbContext.ApplicationSettings.FirstAsync(cancellationToken);

        settings.InactivityTimeoutMinutes = dto.InactivityTimeoutMinutes;

        await this.dbContext.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation(
            "Application settings updated: InactivityTimeoutMinutes={InactivityTimeoutMinutes}",
            dto.InactivityTimeoutMinutes);
    }
}
