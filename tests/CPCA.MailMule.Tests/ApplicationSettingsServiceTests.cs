using CPCA.MailMule.Dtos;
using CPCA.MailMule.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CPCA.MailMule.Tests;

public sealed class ApplicationSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsSeededDefaults()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderAsync();
        var service = serviceProvider.GetRequiredService<IApplicationSettingsService>();

        // Act
        var settings = await service.GetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(30, settings.InactivityTimeoutMinutes);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangedValues()
    {
        // Arrange
        await using var serviceProvider = await BuildServiceProviderAsync();
        var service = serviceProvider.GetRequiredService<IApplicationSettingsService>();

        // Act
        await service.UpdateAsync(new ApplicationSettingsDto(InactivityTimeoutMinutes: 60), TestContext.Current.CancellationToken);
        var settings = await service.GetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(60, settings.InactivityTimeoutMinutes);
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
        db.ApplicationSettings.Add(new ApplicationSettings { Id = 1, InactivityTimeoutMinutes = 30 });
        await db.SaveChangesAsync();

        return sp;
    }
}
