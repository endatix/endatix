using System.Text.Json.Nodes;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

/// <summary>
/// The frozen T-SQL seed of the SQL Server Reporting chain, which has no <c>ON CONFLICT</c> to fall back on and must
/// skip what is already there by itself.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "SqlServer")]
public sealed class ReportingSqlServerSeedTests(DbIntegrationFixture fixture)
{
    // The settings literals of the PostgreSQL seed, which the T-SQL copies.
    private static readonly Dictionary<string, string> SettingsByName = new()
    {
        ["CSV"] = """{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}""",
        ["JSON"] = """{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}""",
        ["Excel (XLSX)"] = """{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}""",
        ["Codebook"] = """{"aliasProfile":"native","keySeparator":"__"}""",
    };

    [Fact]
    public async Task SeedDefaultExportFormatsSqlServerSql_OnMigration_WritesTheSettingsOfThePostgreSqlSeed()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        // Core first, then the reset: the Reporting seed migration reads Tenants, nothing else migrates core when this
        // runs alone, and the reset clears what the core migration seeds.
        await ReportingTestSchema.EnsureCoreMigratedAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);
        await IntegrationTenants.InsertAsync(fixture.Database, cancellationToken);

        // Act
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);

        // Assert
        await using var context = CreateContext();
        var settingsByName = await context.ExportFormats.IgnoreQueryFilters()
            .ToDictionaryAsync(format => format.Name, format => format.SettingsJson!, cancellationToken);
        settingsByName.Keys.Should().BeEquivalentTo(SettingsByName.Keys);
        settingsByName.Should().AllSatisfy(pair =>
            JsonNode.DeepEquals(JsonNode.Parse(pair.Value), JsonNode.Parse(SettingsByName[pair.Key])).Should().BeTrue(pair.Key));
    }

    [Fact]
    public async Task SeedDefaultExportFormatsSqlServerSql_RunTwice_InsertsNothing()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        // Core first, then the reset: the Reporting seed migration reads Tenants, nothing else migrates core when this
        // runs alone, and the reset clears what the core migration seeds.
        await ReportingTestSchema.EnsureCoreMigratedAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);
        await IntegrationTenants.InsertAsync(fixture.Database, cancellationToken);
        await IntegrationTenants.InsertDeletedAsync(fixture.Database, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);
        await using var context = CreateContext();
        var countsAfterMigration = await RowCountsAsync(context, cancellationToken);

        // Act
        await ReportingTestSchema.RunFrozenSeedAsync(context, cancellationToken);

        // Assert
        countsAfterMigration.Should().Be((Formats: 4, Mappings: 1), "the migration seeds the active tenant only");
        (await RowCountsAsync(context, cancellationToken)).Should().Be(countsAfterMigration);
    }

    private ReportingDbContextBase CreateContext() =>
        ReportingTestSchema.CreateContext(fixture.ConnectionString, fixture.Provider, IntegrationTenantContext.Bypass);

    private static async Task<(int Formats, int Mappings)> RowCountsAsync(
        ReportingDbContextBase context,
        CancellationToken cancellationToken) =>
        (await context.ExportFormats.IgnoreQueryFilters().CountAsync(cancellationToken),
            await context.SurveyTypeExportMappings.IgnoreQueryFilters().CountAsync(cancellationToken));
}
