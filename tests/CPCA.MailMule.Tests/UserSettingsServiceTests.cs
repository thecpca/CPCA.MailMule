using CPCA.MailMule.Dtos;
using CPCA.MailMule.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CPCA.MailMule.Tests;

public sealed class UserSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsSeededDefaults()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderAsync();
        var service = serviceProvider.GetRequiredService<IUserSettingsService>();

        // Act
        var settings = await service.GetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(15, settings.UndoWindowSeconds);
        Assert.Equal(25, settings.PageSize);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangedValues()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderAsync();
        var service = serviceProvider.GetRequiredService<IUserSettingsService>();

        // Act
        await service.UpdateAsync(new UserSettingsDto(UndoWindowSeconds: 30, PageSize: 50), TestContext.Current.CancellationToken);
        var settings = await service.GetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(30, settings.UndoWindowSeconds);
        Assert.Equal(50, settings.PageSize);
    }

    private static async Task<ServiceProvider> BuildServiceProviderAsync()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMailMule(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddMailMuleApplication();

        var sp = services.BuildServiceProvider();

        // InMemory provider does not apply HasData seeds — insert the singleton row manually.
        var db = sp.GetRequiredService<MailMuleDbContext>();
        db.UserSettings.Add(new UserSettings { Id = 1, UndoWindowSeconds = 15, PageSize = 25 });
        await db.SaveChangesAsync();

        return sp;
    }
}
