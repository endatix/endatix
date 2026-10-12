using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

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

        await using var factory = new EndatixWebApplicationFactory(
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
    }

    // The ids and EF version the previous build wrote to existing PostgreSQL databases, written out rather than
    // read from the current build, so renaming an applied migration fails here instead of re-running it in production.
    private static readonly (string MigrationId, string ProductVersion)[] PreviousBuildHistory =
    [
        ("20260721062723_InitialReporting", "10.0.0"),
        ("20260904194346_SeedDefaultExportFormats", "10.0.0"),
        ("20260929125737_AddFlattenedSubmissionSourceModifiedAt", "10.0.0"),
        ("20260930070117_AddFlattenedSubmissionSourceRevision", "10.0.0"),
    ];

    [Fact]
    public async Task Host_startup_reapplies_nothing_on_a_database_migrated_by_the_previous_build()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken: cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(
            _fixture.Database.ConnectionString,
            _fixture.Provider,
            cancellationToken);
        await RecordPreviousBuildHistoryAsync(cancellationToken);
        var exportFormatsBefore = await ExportFormatCountAsync(cancellationToken);

        await using var factory = new EndatixWebApplicationFactory(
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
        (await PendingMigrationsAsync(cancellationToken)).Should().BeEmpty();
        (await MigrationHistoryAsync(cancellationToken)).Should().Equal(PreviousBuildHistory);
        (await ExportFormatCountAsync(cancellationToken)).Should().Be(exportFormatsBefore);
    }

    // PostgreSQL SQL from here on: these tests assert the PostgreSQL chain that existing databases hold.
    private async Task RecordPreviousBuildHistoryAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateReportingContext();
        await context.Database.ExecuteSqlRawAsync(
            """DELETE FROM reporting."__EFMigrationsHistory" """,
            cancellationToken);
        foreach (var (migrationId, productVersion) in PreviousBuildHistory)
        {
            await context.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO reporting."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                 VALUES ({migrationId}, {productVersion})
                 """,
                cancellationToken);
        }
    }

    private async Task<List<string>> PendingMigrationsAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateReportingContext();
        return [.. await context.Database.GetPendingMigrationsAsync(cancellationToken)];
    }

    private async Task<List<(string MigrationId, string ProductVersion)>> MigrationHistoryAsync(
        CancellationToken cancellationToken)
    {
        await using var context = CreateReportingContext();
        var rows = await context.Database
            .SqlQueryRaw<string>(
                """SELECT "MigrationId" || ' ' || "ProductVersion" AS "Value" FROM reporting."__EFMigrationsHistory" ORDER BY "MigrationId" """)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.Split(' ')).Select(parts => (parts[0], parts[1]))];
    }

    private async Task<int> ExportFormatCountAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateReportingContext();
        return await context.ExportFormats.IgnoreQueryFilters().CountAsync(cancellationToken);
    }

    private ReportingDbContextBase CreateReportingContext() =>
        ReportingTestSchema.CreateContext(
            _fixture.Database.ConnectionString,
            _fixture.Provider,
            IntegrationTenantContext.Bypass);
}
