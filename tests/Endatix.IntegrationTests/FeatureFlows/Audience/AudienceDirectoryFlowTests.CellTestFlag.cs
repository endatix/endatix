using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The migration that copies each membership's test flag onto its cells (endatix#1376), run down and up on the active
/// provider against cells written before it.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private const string CellTestFlagMigration = "AddPropertyValueIsTest";

    [Fact]
    public async Task Cells_written_before_the_test_flag_moved_onto_them_still_count_for_their_participants()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var form = await FormWithCoverageAsync(admin);
        try
        {
            await MigrateAudienceToBeforeAsync(world.Services, CellTestFlagMigration);

            // Act
            await MigrateAudienceToLatestAsync(world.Services);

            // Assert: the tester's Seats cell counts as test, not as a second real value
            AssertJson(
                """
                {"participants":{"test":1,"real":2},
                 "properties":[
                   {"variableName":"city","filledTest":0,"filledReal":2},
                   {"variableName":"seats","filledTest":1,"filledReal":1},
                   {"variableName":"tier","filledTest":0,"filledReal":0}
                 ]}
                """,
                (await GetVariablesAsync(admin, form.FormId)).GetProperty("coverage"));
        }
        finally
        {
            await MigrateAudienceToLatestAsync(world.Services);
        }
    }

    private static async Task MigrateAudienceToBeforeAsync(IServiceProvider services, string migration)
    {
        await using var scope = services.CreateAsyncScope();
        var audience = (DbContext)scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var ids = audience.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        var index = ids.FindIndex(id => id.EndsWith($"_{migration}", StringComparison.Ordinal));
        Assert.True(index > 0, $"No {migration} migration on {audience.Database.ProviderName}.");
        await audience.GetService<IMigrator>().MigrateAsync(ids[index - 1], TestContext.Current.CancellationToken);
    }

    private static async Task MigrateAudienceToLatestAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var audience = (DbContext)scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        await audience.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }
}
