using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay2Id, seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenIncludeTest_ReturnsAllRows()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        List<long> ids = await CollectIdsAsync(
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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        // Day2 inclusive lower bound, Day3 exclusive upper bound → only production Day2 row.
        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: true,
                CreatedAfter: Day2,
                CreatedBefore: Day3),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay2Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_FiltersByCompletedAtRange_ExclusiveBefore()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        // Production Day1 completed Day2, Day2 completed Day3; Day3 incomplete; test completed Day4.
        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: true,
                CompletedAfter: Day2,
                CompletedBefore: Day4),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay2Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_FiltersBySubmissionIdRange()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        List<long> ids = await CollectIdsAsync(
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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        // Created on/after Day2, production only, id ≤ production Day3 → Day2 and Day3 production rows.
        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: false,
                CreatedAfter: Day2,
                MaxSubmissionId: seed.ProductionDay3Id),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay2Id, seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenIsCompleteTrue_ReturnsOnlyCompleted()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false, IsComplete: true),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay2Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenIsCompleteFalse_ReturnsOnlyIncomplete()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false, IsComplete: false),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenCreatedBeforeExclusiveBound_ExcludesRowAtBound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        // CreatedBefore is exclusive: Day2 row created at Day2 must be excluded.
        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: false,
                CreatedBefore: Day2),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenCompletedAtNull_ExcludesFromCompletedAtRange()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        // Incomplete Day3 has null CompletedAt and must never match a completed-at range.
        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: true,
                CompletedAfter: Day1,
                CompletedBefore: Day4.AddDays(1)),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay2Id, seed.TestDay3Id);
        ids.Should().NotContain(seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_ExcludesSoftDeletedFlattenedRows()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        FlattenedSubmission day2 = await reportingDb.FlattenedSubmissions
            .SingleAsync(row => row.SubmissionId == seed.ProductionDay2Id, cancellationToken);
        day2.MarkDeleted();
        await reportingDb.SaveChangesAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay3Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_ExcludesNonProcessedFlattenedRows()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        FlattenedSubmission day1 = await reportingDb.FlattenedSubmissions
            .SingleAsync(row => row.SubmissionId == seed.ProductionDay1Id, cancellationToken);
        day1.MarkFailed("flatten failed");
        await reportingDb.SaveChangesAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay2Id, seed.ProductionDay3Id);
    }

    [Fact]
    public async Task HasExportableRowsAsync_WhenFiltersMatchNothing_ReturnsFalse()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        bool hasRows = await repository.HasExportableRowsAsync(
            TenantId,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: false,
                CreatedAfter: Day4),
            cancellationToken);

        hasRows.Should().BeFalse();
    }

    [Fact]
    public async Task HasExportableRowsAsync_WhenRowsExist_ReturnsTrue()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        bool hasRows = await repository.HasExportableRowsAsync(
            TenantId,
            seed.FormId,
            new ExportQueryOptions(IncludeTestSubmissions: false),
            cancellationToken);

        hasRows.Should().BeTrue();
    }

    [Fact]
    public async Task HasCompletedSubmissionsAsync_WhenCompletedExist_ReturnsTrue()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        bool hasCompleted = await repository.HasCompletedSubmissionsAsync(
            TenantId,
            seed.FormId,
            cancellationToken);

        hasCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_ProjectsStartedAtFromCoreSubmission()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        List<FlattenedExportRow> rows = [];
        await foreach (FlattenedExportRow row in repository.StreamFlattenedSubmissionsAsync(
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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        // Day2 inclusive lower bound, Day3 exclusive upper bound → only production Day2 row.
        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: true,
                StartedAfter: Day2,
                StartedBefore: Day3),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay2Id);
    }

    [Fact]
    public async Task StreamFlattenedSubmissionsAsync_WhenStartedAtNull_ExcludesFromStartedAtRange()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededExportFixture seed = await SeedExportFixtureAsync(cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        // Clear start on incomplete production Day3 — range filters must exclude null StartedAt.
        await appDb.Submissions
            .Where(row => row.Id == seed.ProductionDay3Id)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(row => row.StartedAt, (DateTime?)null),
                cancellationToken);

        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        ReportingExportRepository repository = CreateRepository(reportingDb, appDb);

        List<long> ids = await CollectIdsAsync(
            repository,
            seed.FormId,
            new ExportQueryOptions(
                IncludeTestSubmissions: false,
                StartedAfter: Day1),
            cancellationToken);

        ids.Should().Equal(seed.ProductionDay1Id, seed.ProductionDay2Id);
        ids.Should().NotContain(seed.ProductionDay3Id);
    }

    private async Task<SeededExportFixture> SeedExportFixtureAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        Tenant tenant = new("export-filter-tenant") { Id = TenantId };
        appDb.Set<Tenant>().Add(tenant);
        await appDb.SaveChangesAsync(cancellationToken);

        // Two-step form+definition save avoids Form ↔ ActiveDefinition circular insert.
        Form form = new(TenantId, "Export filter form");
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(cancellationToken);

        FormDefinition definition = new(TenantId, isDraft: false, jsonData: """{"pages":[]}""");
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(cancellationToken);

        long formId = form.Id;
        long formDefinitionId = definition.Id;

        // Seed matrix (ids assigned by AppDbContext):
        // production Day1 / started Day1 / completed Day2
        // production Day2 / started Day2 / completed Day3
        // production Day3 / started Day3 / incomplete
        // test Day3 / started Day3 / completed Day4
        long productionDay1Id = await SeedSubmissionAsync(
            appDb, formId, formDefinitionId, isTest: false, isComplete: true,
            createdAt: Day1, startedAt: Day1, completedAt: Day2, cancellationToken);
        long productionDay2Id = await SeedSubmissionAsync(
            appDb, formId, formDefinitionId, isTest: false, isComplete: true,
            createdAt: Day2, startedAt: Day2, completedAt: Day3, cancellationToken);
        long productionDay3Id = await SeedSubmissionAsync(
            appDb, formId, formDefinitionId, isTest: false, isComplete: false,
            createdAt: Day3, startedAt: Day3, completedAt: null, cancellationToken);
        long testDay3Id = await SeedSubmissionAsync(
            appDb, formId, formDefinitionId, isTest: true, isComplete: true,
            createdAt: Day3, startedAt: Day3, completedAt: Day4, cancellationToken);

        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        foreach (long submissionId in new[] { productionDay1Id, productionDay2Id, productionDay3Id, testDay3Id })
        {
            FlattenedSubmission row = new(submissionId, TenantId, formId);
            row.MarkProcessed($$"""{"submissionId":{{submissionId}}}""");
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
        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: formId,
            FormDefinitionId: formDefinitionId,
            JsonData: """{"answer":"x"}""",
            IsComplete: isComplete,
            IsTestSubmission: isTest));

        appDb.Submissions.Add(submission);
        await appDb.SaveChangesAsync(cancellationToken);
        long submissionId = submission.Id;

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
        await foreach (FlattenedExportRow row in repository.StreamFlattenedSubmissionsAsync(
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
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);

        IncrementingIdGenerator idGenerator = new();
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigurePostgreSqlOptions(optionsBuilder, _fixture.ConnectionString);

        return new AppDbContext(
            optionsBuilder.Options,
            idGenerator,
            tenantContext,
            new EfCoreValueGeneratorFactory(idGenerator),
            new OutboxIntegrationEventDispatcher());
    }

    private ReportingDbContext CreateReportingDbContext()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);

        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, new IncrementingIdGenerator(), tenantContext);
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
