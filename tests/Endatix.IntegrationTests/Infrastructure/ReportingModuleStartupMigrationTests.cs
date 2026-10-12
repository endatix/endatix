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
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        response.Should().BeSuccessful();
        var exportFormatsTableExists = await IntegrationDbAssert.TableExistsAsync(
            _fixture.Database.ConnectionString,
            _fixture.Database.Provider,
            schema: "reporting",
            table: "ExportFormats",
            cancellationToken);
        Assert.True(exportFormatsTableExists);
    }

    // The ids and EF version the shipped builds wrote to existing PostgreSQL databases, written out rather than
    // read from the current build, so renaming an applied migration fails here instead of re-running it in production.
    private static readonly (string MigrationId, string ProductVersion)[] ShippedHistory =
    [
        ("20260721062723_InitialReporting", "10.0.0"),
        ("20260904194346_SeedDefaultExportFormats", "10.0.0"),
        ("20260929125737_AddFlattenedSubmissionSourceModifiedAt", "10.0.0"),
        ("20260930070117_AddFlattenedSubmissionSourceRevision", "10.0.0"),
    ];

    [Fact]
    public async Task Host_startup_on_a_database_carrying_the_shipped_migration_history_applies_nothing_and_keeps_those_rows()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken: cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(
            _fixture.Database.ConnectionString,
            _fixture.Provider,
            cancellationToken);
        await RecordShippedHistoryAsync(cancellationToken);

        await using var factory = new EndatixWebApplicationFactory(
                _fixture.Database.ConnectionString,
                _fixture.Database.Provider)
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Endatix:FeatureFlags:ReportingModule", "true");
            });

        // Act
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        response.Should().BeSuccessful();
        var history = await MigrationHistoryAsync(cancellationToken);
        (await PendingMigrationsAsync(cancellationToken)).Should().BeEmpty();
        history.Should().Contain(ShippedHistory);
        history.Select(row => row.MigrationId).Should().BeSubsetOf(MigrationsOfThisBuild());
    }

    // PostgreSQL SQL from here on: these tests assert the PostgreSQL chain that existing databases hold.

    // Rewrites the shipped part of the history exactly as the shipped builds wrote it. Rows of migrations added after
    // them (ids that sort later) are this build's own and stay applied, so a new migration does not make it pending.
    private async Task RecordShippedHistoryAsync(CancellationToken cancellationToken)
    {
        var lastShippedId = ShippedHistory[^1].MigrationId;
        await using var context = CreateReportingContext();
        await context.Database.ExecuteSqlAsync(
            $"""DELETE FROM reporting."__EFMigrationsHistory" WHERE "MigrationId" <= {lastShippedId}""",
            cancellationToken);
        foreach (var (migrationId, productVersion) in ShippedHistory)
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

    private List<string> MigrationsOfThisBuild()
    {
        using var context = CreateReportingContext();
        return [.. context.Database.GetMigrations()];
    }

    private ReportingDbContextBase CreateReportingContext() =>
        ReportingTestSchema.CreateContext(
            _fixture.Database.ConnectionString,
            _fixture.Provider,
            IntegrationTenantContext.Bypass);
}
