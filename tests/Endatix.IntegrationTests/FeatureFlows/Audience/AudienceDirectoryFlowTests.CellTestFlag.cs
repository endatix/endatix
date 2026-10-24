using System.Net;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Each cell carries its membership's test flag (endatix#1376): every write path sets it, and the migration that
/// copies it onto cells written before it runs down and up on the active provider.
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

    [Fact]
    public async Task Every_write_path_gives_a_cell_its_participants_test_flag()
    {
        // Arrange: added with values, imported as test and as real, and a value saved later
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var seats = await admin.CreatePropertyAsync(formId, new { name = "Seats", dataType = "number" });
        await AddPersonWithValuesAsync(admin, formId, new("ada@example.com", IsTest: false, new() { [city] = "Sofia" }));
        var tester = await AddPersonWithValuesAsync(
            admin, formId, new("tester@example.com", IsTest: true, new() { [city] = "Varna" }));
        foreach (var (identifier, isTest) in new[] { ("imported-tester@example.com", true), ("bob@example.com", false) })
        {
            using var imported = await admin.PostAsync(
                $"/api/forms/{formId}/audience/import",
                new
                {
                    csvText = $"email,city\n{identifier},Ruse\n",
                    identifierColumn = "email",
                    fileName = "people.csv",
                    propertyColumns = new Dictionary<string, string> { ["city"] = "city" },
                    isTest,
                });
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        }

        // Act
        using var saved = await admin.PutValueAsync(
            $"/api/forms/{formId}/audience/people/{tester.MembershipId}", seats, "2");

        // Assert: four participants' cells, and none whose flag differs from its participant's
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var cells = await CellFlagsAsync(world.Services, formId);
        Assert.Equal(5, cells.Count);
        Assert.Equal(3, cells.Count(cell => cell.MembershipIsTest));
        Assert.All(cells, cell => Assert.Equal(cell.MembershipIsTest, cell.CellIsTest));
    }

    private sealed record CellFlag(bool CellIsTest, bool MembershipIsTest);

    /// <summary>Every live cell of the form with its own test flag and its membership's.</summary>
    private static async Task<List<CellFlag>> CellFlagsAsync(IServiceProvider services, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        return await db.PropertyValues.IgnoreQueryFilters()
            .Where(cell => !cell.IsDeleted)
            .Join(
                db.Memberships.IgnoreQueryFilters().Where(membership => membership.FormId == formId),
                cell => cell.MembershipId,
                membership => membership.Id,
                (cell, membership) => new CellFlag(cell.IsTest, membership.IsTest))
            .ToListAsync(TestContext.Current.CancellationToken);
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
