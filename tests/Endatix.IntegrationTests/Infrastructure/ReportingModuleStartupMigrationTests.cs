using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class ReportingModuleStartupMigrationTests
{
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public ReportingModuleStartupMigrationTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Host_startup_migrates_reporting_module_when_feature_flag_enabled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken: cancellationToken);

        var factory = new EndatixWebApplicationFactory(
                _fixture.Database.ConnectionString,
                _fixture.Database.Provider)
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Endatix:FeatureFlags:ReportingModule", "true");
            });

        // Act
        using var client = factory.CreateClient();
        _ = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        var exportFormatsTableExists = await IntegrationDbAssert.TableExistsAsync(
            _fixture.Database.ConnectionString,
            _fixture.Database.Provider,
            schema: "reporting",
            table: "ExportFormats",
            cancellationToken);
        Assert.True(exportFormatsTableExists);

        await factory.DisposeAsync();
    }
}
