using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// PostgreSQL integration coverage for export request-time filters in
/// <see cref="ReportingExportRepository"/> (date ranges including StartedAt, test inclusion, submission id range).
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class ReportingExportRepositoryIntegrationTests
{
    private const long TenantId = 41;

    private static readonly DateTime Day1 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day3 = new(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day4 = new(2026, 1, 4, 12, 0, 0, DateTimeKind.Utc);

    private readonly DbIntegrationFixture _fixture;

    public ReportingExportRepositoryIntegrationTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_ExcludesTestSubmissionsByDefault()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay2Id, seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenIncludeTest_ReturnsAllRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: true),
            cancellationToken);

        ids.Should().Equal(
            seed.ProductionDay1Id,
            seed.ProductionDay2Id,
            seed.ProductionDay3Id,
            seed.TestDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_FiltersByCreatedAtRange_ExclusiveBefore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        // Day2 inclusive lower bound, Day3 exclusive upper bound → only production Day2 row.
        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: true,
                Created: new UtcDateTimeRange(Day2, Day3)),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay2Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_FiltersByCompletedAtRange_ExclusiveBefore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        // Production Day1 completed Day2, Day2 completed Day3; Day3 incomplete; test completed Day4.
        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: true,
                Completed: new UtcDateTimeRange(Day2, Day4)),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay2Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_FiltersBySubmissionIdRange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: true,
                MinSubmissionId: seed.ProductionDay2Id,
                MaxSubmissionId: seed.ProductionDay3Id),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay2Id, seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_CombinesFiltersWithAnd()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        // Created on/after Day2, production only, id ≤ production Day3 → Day2 and Day3 production rows.
        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: false,
                Created: new UtcDateTimeRange(Day2, null),
                MaxSubmissionId: seed.ProductionDay3Id),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay2Id, seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenIsCompleteTrue_ReturnsOnlyCompleted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false, IsComplete: true),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay2Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenIsCompleteFalse_ReturnsOnlyIncomplete()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false, IsComplete: false),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenCreatedToExclusiveBound_ExcludesRowAtBound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        // CreatedTo is exclusive: Day2 row created at Day2 must be excluded.
        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: false,
                Created: new UtcDateTimeRange(null, Day2)),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenCompletedAtNull_ExcludesFromCompletedAtRange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        // Incomplete Day3 has null CompletedAt and must never match a completed-at range.
        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: true,
                Completed: new UtcDateTimeRange(Day1, Day4.AddDays(1))),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay2Id, seed.TestDay3Id);
        ids.Should().NotContain(seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_ExcludesSoftDeletedFlattenedRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var reportingDb = CreateReportingDbContext();
        var day2 = await reportingDb.FlattenedSubmissions
            .SingleAsync(row => row.SubmissionId == seed.ProductionDay2Id, cancellationToken);
        day2.MarkDeleted();
        await reportingDb.SaveChangesAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_ExcludesNonProcessedFlattenedRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var reportingDb = CreateReportingDbContext();
        var day1 = await reportingDb.FlattenedSubmissions
            .SingleAsync(row => row.SubmissionId == seed.ProductionDay1Id, cancellationToken);
        day1.MarkFailed("flatten failed");
        await reportingDb.SaveChangesAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay2Id, seed.ProductionDay3Id);
    }

    [Fact]
    public async Task HasExportableRowsAsync_WhenFiltersMatchNothing_ReturnsFalse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var hasRows = await repository.HasExportableRowsAsync(
            TenantId,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: false,
                Created: new UtcDateTimeRange(Day4, null)),
            cancellationToken);

        hasRows.Should().BeFalse();
    }

    [Fact]
    public async Task HasExportableRowsAsync_WhenRowsExist_ReturnsTrue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var hasRows = await repository.HasExportableRowsAsync(
            TenantId,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false),
            cancellationToken);

        hasRows.Should().BeTrue();
    }

    [Fact]
    public async Task HasCompletedSubmissionsAsync_WhenCompletedExist_ReturnsTrue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var hasCompleted = await repository.HasCompletedSubmissionsAsync(
            TenantId,
            seed.FormId,
            cancellationToken);

        hasCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_ProjectsStartedAtFromCoreSubmission()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        List<FlattenedExportRow> rows = [];
        await foreach (var row in repository.StreamFlattenedSubmissionsAsync(
                           TenantId,
                           seed.FormId,
                           new ExportQueryOptions(IncludeTestSubmissions: false),
                           cancellationToken))
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(3);
        rows.Single(row => row.SubmissionId == seed.ProductionDay1Id).StartedAt.Should().Be(Day1);
        rows.Single(row => row.SubmissionId == seed.ProductionDay2Id).StartedAt.Should().Be(Day2);
        rows.Single(row => row.SubmissionId == seed.ProductionDay3Id).StartedAt.Should().Be(Day3);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_FiltersByStartedAtRange_ExclusiveBefore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        // Day2 inclusive lower bound, Day3 exclusive upper bound → only production Day2 row.
        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: true,
                Started: new UtcDateTimeRange(Day2, Day3)),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay2Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenStartedAtNull_ExcludesFromStartedAtRange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedExportFixtureAsync(cancellationToken);

        await using var appDb = CreateAppDbContext();
        // Clear start on incomplete production Day3 — range filters must exclude null StartedAt.
        await appDb.Submissions
            .Where(row => row.Id == seed.ProductionDay3Id)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(row => row.StartedAt, (DateTime?)null),
                cancellationToken);

        await using var reportingDb = CreateReportingDbContext();
        var repository = CreateRepository(reportingDb, appDb);

        var ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: false,
                Started: new UtcDateTimeRange(Day1, null)),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay2Id);
        ids.Should().NotContain(seed.ProductionDay3Id);
    }

    private async Task<SeededExportFixture> SeedExportFixtureAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        await using var appDb = CreateAppDbContext();
        Tenant tenant = new("export-filter-tenant", "tnntexpf") { Id = TenantId };
        appDb.Set<Tenant>().Add(tenant);
        await appDb.SaveChangesAsync(cancellationToken);

        // Two-step form+definition save avoids Form ↔ ActiveDefinition circular insert.
        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "Export filter form"));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(cancellationToken);

        FormDefinition definition = new(TenantId, isDraft: false, jsonData: """{"pages":[]}""");
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(cancellationToken);

        var formId = form.Id;
        var formDefinitionId = definition.Id;

        // Seed matrix (ids assigned by AppDbContext):
        // production Day1 / started Day1 / completed Day2
        // production Day2 / started Day2 / completed Day3
        // production Day3 / started Day3 / incomplete
        // test Day3 / started Day3 / completed Day4
        var productionDay1Id = await SeedSubmissionAsync(
            appDb, formId, formDefinitionId, isTest: false, isComplete: true,
            createdAt: Day1, startedAt: Day1, completedAt: Day2, cancellationToken);
        var productionDay2Id = await SeedSubmissionAsync(
            appDb, formId, formDefinitionId, isTest: false, isComplete: true,
            createdAt: Day2, startedAt: Day2, completedAt: Day3, cancellationToken);
        var productionDay3Id = await SeedSubmissionAsync(
            appDb, formId, formDefinitionId, isTest: false, isComplete: false,
            createdAt: Day3, startedAt: Day3, completedAt: null, cancellationToken);
        var testDay3Id = await SeedSubmissionAsync(
            appDb, formId, formDefinitionId, isTest: true, isComplete: true,
            createdAt: Day3, startedAt: Day3, completedAt: Day4, cancellationToken);

        await using var reportingDb = CreateReportingDbContext();
        foreach (var submissionId in new[] { productionDay1Id, productionDay2Id, productionDay3Id, testDay3Id })
        {
            FlattenedSubmission row = new(submissionId, TenantId, formId);
            row.MarkProcessed($$"""{"submissionId":{{submissionId}}}""", DateTime.UtcNow);
            reportingDb.FlattenedSubmissions.Add(row);
        }

        await reportingDb.SaveChangesAsync(cancellationToken);
        return new SeededExportFixture(
            formId,
            formDefinitionId,
            productionDay1Id,
            productionDay2Id,
            productionDay3Id,
            testDay3Id);
    }

    private static async Task<long> SeedSubmissionAsync(
        AppDbContext appDb,
        long formId,
        long formDefinitionId,
        bool isTest,
        bool isComplete,
        DateTime createdAt,
        DateTime? startedAt,
        DateTime? completedAt,
        CancellationToken cancellationToken)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: formId,
            FormDefinitionId: formDefinitionId,
            JsonData: """{"answer":"x"}""",
            IsComplete: isComplete,
            IsTestSubmission: isTest));

        appDb.Submissions.Add(submission);
        await appDb.SaveChangesAsync(cancellationToken);
        var submissionId = submission.Id;

        // OwnsOne Status uses shared static instances; clear tracking before the next Add.
        appDb.ChangeTracker.Clear();

        // Stamp filter-relevant timestamps after insert.
        await appDb.Submissions
            .Where(row => row.Id == submissionId)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(row => row.CreatedAt, createdAt)
                    .SetProperty(row => row.StartedAt, startedAt)
                    .SetProperty(row => row.CompletedAt, completedAt),
                cancellationToken);

        return submissionId;
    }

    private static async Task<List<long>> CollectIdsAsync(
        ReportingExportRepository repository,
        long formId,
        ExportQueryOptions options,
        CancellationToken cancellationToken)
    {
        List<long> ids = [];
        await foreach (var row in repository.StreamFlattenedSubmissionsAsync(
                           TenantId,
                           formId,
                           options,
                           cancellationToken))
        {
            ids.Add(row.SubmissionId);
        }

        return ids;
    }

    private AppDbContext CreateAppDbContext()
    {
        IntegrationTenantContext tenantContext = new(TenantId);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigurePostgreSqlOptions(optionsBuilder, _fixture.ConnectionString);

        return new AppDbContext(
            optionsBuilder.Options,
            tenantContext,
            new OutboxIntegrationEventDispatcher());
    }

    private ReportingDbContext CreateReportingDbContext()
    {
        IntegrationTenantContext tenantContext = new(TenantId);

        var optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, tenantContext);
    }

    private static ReportingExportRepository CreateRepository(
        ReportingDbContext reportingDb,
        AppDbContext appDb) =>
        new(reportingDb, appDb, NullLogger<ReportingExportRepository>.Instance);

    private sealed record SeededExportFixture(
        long FormId,
        long FormDefinitionId,
        long ProductionDay1Id,
        long ProductionDay2Id,
        long ProductionDay3Id,
        long TestDay3Id);
}
