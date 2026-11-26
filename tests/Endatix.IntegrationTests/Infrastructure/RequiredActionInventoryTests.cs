using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using static Endatix.IntegrationTests.RegisteredOperations;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed partial class RequiredActionInventoryTests(EndatixIntegrationWebHostFixture fixture)
{
    [Theory]
    [InlineData("core")]
    [InlineData("jobs")]
    [InlineData("audience")]
    [InlineData("reporting")]
    [InlineData("dashboard-read")]
    public async Task Registered_operations_match_the_reviewed_inventory(string profile)
    {
        // Arrange
        var modules = ModulesIn(profile);
        await using var factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        await using var host = factory.WithWebHostBuilder(builder => ConfigureProfile(builder, profile));

        // Act
        var registrations = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.Cast<RouteEndpoint>();
        var differences = Differences(Operations(registrations), Reviewed(modules));

        // Assert
        differences.Should().BeEmpty("registered operations must match Snapshots/required-action-inventory.txt");
    }

    private static void ConfigureProfile(IWebHostBuilder builder, string profile)
    {
        var modules = ModulesIn(profile);
        builder.UseSetting("Endatix:FeatureFlags:JobsModule", modules.Contains("jobs").ToString());
        builder.UseSetting("Endatix:FeatureFlags:PersonalizationModule", modules.Contains("audience").ToString());
        builder.UseSetting("Endatix:FeatureFlags:ReportingModule", modules.Contains("reporting").ToString());
        builder.UseSetting("Endatix:FeatureFlags:MultiTenancy", modules.Contains("dashboard").ToString());
        builder.UseSetting("Endatix:BackgroundJobs:Dashboard:Enabled", modules.Contains("dashboard").ToString());
        builder.UseSetting("Endatix:BackgroundJobs:Dashboard:AllowWrites", (profile == "dashboard-write").ToString());
    }
}
