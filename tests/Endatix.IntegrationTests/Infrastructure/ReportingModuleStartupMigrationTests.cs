extern alias EndatixWebHost;

using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class ReportingModuleStartupMigrationTests
{
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public ReportingModuleStartupMigrationTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    private const string SubmissionsSettingsJson =
        """{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}""";

    private const string CodebookSettingsJson = """{"aliasProfile":"native","keySeparator":"__"}""";

    private static readonly ExportFormatSettingsParser SettingsParser = new(NullLogger<ExportFormatSettingsParser>.Instance);

    // The rows the frozen seeds insert, written out rather than read from the runtime catalog, which may change while
    // applied seeds may not. Settings are compared as parsed: PostgreSQL's first migration seeded CSV, JSON and
    // Codebook with a "locale" key, request-time only since, and the Codebook without the defaults written here.
    private static readonly SeededFormat[] SeededFormats =
    [
        new("CSV", ExportTarget.Submissions, ExportDeliveryFormat.Csv,
            "Default CSV export for form submissions", SettingsParser.Parse(SubmissionsSettingsJson)),
        new("JSON", ExportTarget.Submissions, ExportDeliveryFormat.Json,
            "Default JSON export for form submissions", SettingsParser.Parse(SubmissionsSettingsJson)),
        new("Excel (XLSX)", ExportTarget.Submissions, ExportDeliveryFormat.Xlsx,
            "Default Excel export for form submissions", SettingsParser.Parse(SubmissionsSettingsJson)),
        new("Codebook", ExportTarget.Codebook, ExportDeliveryFormat.Json,
            "Default form definition codebook export", SettingsParser.Parse(CodebookSettingsJson)),
    ];

    [Fact]
    public async Task Host_startup_migrates_reporting_module_when_feature_flag_enabled()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var restoreSchema = await ResetWithoutReportingSchemaAsync(cancellationToken);
        await using var factory = CreateFactory(applyMigrationsAtStartup: true);

        // Act
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        response.Should().BeSuccessful();
        foreach (var table in new[] { "ExportFormats", "FlattenedSubmissions", "FormSchemas", "SurveyTypeExportMappings" })
        {
            (await ReportingTableExistsAsync(table, cancellationToken)).Should().BeTrue($"startup must create {table}");
        }

        (await AppliedMigrationsAsync(cancellationToken)).Should().Equal(
            MigrationsOfThisBuild(),
            "startup must record every migration of the provider's chain, and no other");
    }

    [Fact]
    public async Task Host_startup_skips_reporting_migrations_when_startup_migration_is_off()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var restoreSchema = await ResetWithoutReportingSchemaAsync(cancellationToken);
        await using var factory = CreateFactory(applyMigrationsAtStartup: false);

        // Act
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        response.Should().BeSuccessful();
        (await ReportingTableExistsAsync("ExportFormats", cancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Reporting_migrations_seed_default_export_formats_for_existing_tenants()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken: cancellationToken);
        var activeTenantId = await IntegrationTenants.InsertAsync(_fixture.Database, cancellationToken);
        var deletedTenantId = await IntegrationTenants.InsertDeletedAsync(_fixture.Database, cancellationToken);

        // Act
        await ReportingTestSchema.EnsureMigratedAsync(
            _fixture.Database.ConnectionString,
            _fixture.Provider,
            cancellationToken);

        // Assert
        await using var context = CreateReportingContext();
        var formats = await context.ExportFormats.IgnoreQueryFilters()
            .Where(format => format.TenantId == activeTenantId)
            .ToListAsync(cancellationToken);
        formats.Select(SeededFormat.Of).Should().BeEquivalentTo(SeededFormats);
        formats.Should().OnlyContain(format => format.Id > 0 && !format.IsDeleted);

        var mappings = await context.SurveyTypeExportMappings.IgnoreQueryFilters()
            .Where(mapping => mapping.TenantId == activeTenantId)
            .ToListAsync(cancellationToken);
        var mapping = mappings.Should().ContainSingle().Subject;
        mapping.SurveyTypeId.Should().BeNull();
        mapping.IsDefault.Should().BeTrue();
        mapping.ExportFormatId.Should().Be(formats.Single(format => format.Name == "CSV").Id);

        (await context.ExportFormats.IgnoreQueryFilters().CountAsync(format => format.TenantId == deletedTenantId, cancellationToken))
            .Should().Be(0);
        (await context.SurveyTypeExportMappings.IgnoreQueryFilters().CountAsync(row => row.TenantId == deletedTenantId, cancellationToken))
            .Should().Be(0);
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

    private static string LastShippedId => ShippedHistory[^1].MigrationId;

    // PostgreSQL only: it replays the history the shipped PostgreSQL chain wrote (array parameters, || and COLLATE "C").
    [Fact]
    [Trait("DbSpecific", "PostgreSql")]
    public async Task Host_startup_upgrades_a_database_stopped_at_the_last_shipped_migration_and_reruns_nothing()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken: cancellationToken);
        await IntegrationTenants.InsertAsync(_fixture.Database, cancellationToken);
        await MigrateToLastShippedAsync(cancellationToken);
        await RecordShippedHistoryAsync(cancellationToken);
        var idsBefore = (await MigrationHistoryAsync(cancellationToken)).Select(row => row.MigrationId).ToList();
        var newerMigrations = MigrationsOfThisBuild().Where(id => string.CompareOrdinal(id, LastShippedId) > 0).ToList();
        var exportFormatsBefore = await ExportFormatCountAsync(cancellationToken);
        exportFormatsBefore.Should().BePositive("the tenant's seeded formats are what an unchanged count after startup is compared against");

        await using var factory = CreateFactory(applyMigrationsAtStartup: true);

        // Act
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        response.Should().BeSuccessful();
        var history = await MigrationHistoryAsync(cancellationToken);
        idsBefore.Intersect(newerMigrations).Should().BeEmpty("the database stops where the last shipped build left it");
        history.Select(row => row.MigrationId).Should().Equal(
            idsBefore.Concat(newerMigrations).Order(StringComparer.Ordinal),
            "startup must apply this build's newer migrations and re-run nothing, including the idempotent seed");
        history.Should().Contain(ShippedHistory, "startup must leave the shipped rows as the shipped builds wrote them");
        history.Select(row => row.MigrationId).Should().BeSubsetOf(MigrationsOfThisBuild());
        (await PendingMigrationsAsync(cancellationToken)).Should().BeEmpty();
        (await ExportFormatCountAsync(cancellationToken)).Should().Be(exportFormatsBefore, "startup must not add or remove export formats");
    }

    // PostgreSQL SQL from here on: these tests assert the PostgreSQL chain that existing databases hold.

    // Migrating to the last shipped id leaves the database where an upgrading one starts. A migration of this build
    // with an older id (one merged from a long-lived branch) is applied here too, because EF applies in id order.
    private async Task MigrateToLastShippedAsync(CancellationToken cancellationToken)
    {
        await using var context = await ReportingTestSchema.DropSchemaAsync(
            _fixture.Database.ConnectionString,
            _fixture.Provider,
            cancellationToken);
        await context.Database.MigrateAsync(LastShippedId, cancellationToken);
    }

    // Rewrites exactly the shipped rows as the shipped builds wrote them: their literal ids, not this build's, so a
    // renamed shipped migration leaves an id the build does not know, and their EF version.
    private async Task RecordShippedHistoryAsync(CancellationToken cancellationToken)
    {
        string[] shippedIds = [.. ShippedHistory.Select(row => row.MigrationId)];
        await using var context = CreateReportingContext();
        await context.Database.ExecuteSqlAsync(
            $"""DELETE FROM reporting."__EFMigrationsHistory" WHERE "MigrationId" = ANY({shippedIds})""",
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

    // Respawn keeps schema objects, so the Reporting tables an earlier test created would otherwise still be there.
    // Disposing the result puts the schema back, whatever the test left.
    private async Task<RecreatedReportingSchema> ResetWithoutReportingSchemaAsync(CancellationToken cancellationToken)
    {
        await _fixture.ResetDatabaseAsync(cancellationToken: cancellationToken);
        await using var context = await ReportingTestSchema.DropSchemaAsync(
            _fixture.Database.ConnectionString,
            _fixture.Provider,
            cancellationToken);
        return new RecreatedReportingSchema(_fixture);
    }

    private WebApplicationFactory<EndatixWebHost::Program> CreateFactory(bool applyMigrationsAtStartup) =>
        new EndatixWebApplicationFactory(_fixture.Database.ConnectionString, _fixture.Database.Provider)
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Endatix:FeatureFlags:ReportingModule", "true");
                builder.UseSetting(
                    "Customizations:Reporting:ApplyMigrationsAtStartup",
                    applyMigrationsAtStartup.ToString());
            });

    private Task<bool> ReportingTableExistsAsync(string table, CancellationToken cancellationToken) =>
        IntegrationDbAssert.TableExistsAsync(
            _fixture.Database.ConnectionString,
            _fixture.Database.Provider,
            schema: "reporting",
            table,
            cancellationToken);

    private async Task<List<string>> AppliedMigrationsAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateReportingContext();
        return [.. await context.Database.GetAppliedMigrationsAsync(cancellationToken)];
    }

    private async Task<int> ExportFormatCountAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateReportingContext();
        return await context.ExportFormats.IgnoreQueryFilters().CountAsync(cancellationToken);
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
                """SELECT "MigrationId" || ' ' || "ProductVersion" AS "Value" FROM reporting."__EFMigrationsHistory" ORDER BY "MigrationId" COLLATE "C" """)
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

    private sealed record SeededFormat(
        string Name,
        ExportTarget Target,
        ExportDeliveryFormat Delivery,
        string Description,
        ExportFormatSettings Settings,
        ExportProfile Profile = ExportProfile.Native)
    {
        public static SeededFormat Of(ExportFormat format) =>
            new(
                format.Name,
                format.ExportTarget,
                format.DeliveryFormat,
                format.Description!,
                SettingsParser.Parse(format.SettingsJson),
                format.Profile);
    }

    // Data resets delete from every Reporting table they found, so a test that leaves the schema dropped puts it back.
    private sealed class RecreatedReportingSchema(EndatixIntegrationWebHostFixture fixture) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() =>
            await ReportingTestSchema.EnsureMigratedAsync(fixture.Database.ConnectionString, fixture.Provider);
    }
}
