namespace CPCA.MailMule.Services;

using CPCA.MailMule.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

internal sealed class UserSettingsService : IUserSettingsService
{
    private readonly MailMuleDbContext dbContext;
    private readonly ILogger<UserSettingsService> logger;

    public UserSettingsService(MailMuleDbContext dbContext, ILogger<UserSettingsService> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await this.dbContext.UserSettings.FirstAsync(cancellationToken);
        return new UserSettingsDto(settings.UndoWindowSeconds, settings.PageSize);
    }

    public async Task UpdateAsync(UserSettingsDto dto, CancellationToken cancellationToken = default)
    {
        var settings = await this.dbContext.UserSettings.FirstAsync(cancellationToken);

        settings.UndoWindowSeconds = dto.UndoWindowSeconds;
        settings.PageSize = dto.PageSize;

        await this.dbContext.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation(
            "User settings updated: UndoWindowSeconds={UndoWindowSeconds}, PageSize={PageSize}",
            dto.UndoWindowSeconds,
            dto.PageSize);
    }
}
