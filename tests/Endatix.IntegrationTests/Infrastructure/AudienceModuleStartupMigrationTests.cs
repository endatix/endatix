using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Host startup creates the audience schema on whichever provider the run uses.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class AudienceModuleStartupMigrationTests(EndatixIntegrationWebHostFixture fixture)
{
    private static readonly string[] AudienceTables =
        ["Imports", "Links", "Members", "Memberships", "Properties", "PropertyValues", "Settings"];

    [Fact]
    public async Task Host_startup_migrates_audience_module_when_feature_flag_enabled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var audience = scope.ServiceProvider.GetRequiredService<AudienceDbContextBase>();
            await audience.Database.MigrateAsync(Migration.InitialDatabase, cancellationToken);
        }

        var tablesBefore = await ExistingAudienceTablesAsync(cancellationToken);
        await using var factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Provider);

        // Act
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        response.Should().BeSuccessful();
        tablesBefore.Should().BeEmpty("reverting every audience migration leaves no audience table behind");
        (await ExistingAudienceTablesAsync(cancellationToken)).Should().BeEquivalentTo(AudienceTables);
        await using var verify = factory.Services.CreateAsyncScope();
        var context = verify.ServiceProvider.GetRequiredService<AudienceDbContextBase>();
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        applied.Should().Equal(context.Database.GetMigrations(), "startup applies this provider's whole chain, once");
    }

    private async Task<List<string>> ExistingAudienceTablesAsync(CancellationToken cancellationToken)
    {
        List<string> existing = [];
        foreach (var table in AudienceTables)
        {
            if (await IntegrationDbAssert.TableExistsAsync(
                    fixture.Database.ConnectionString,
                    fixture.Provider,
                    AudiencePersistence.Schema,
                    table,
                    cancellationToken))
            {
                existing.Add(table);
            }
        }

        return existing;
    }
}
