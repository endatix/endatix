using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.IntegrationTests;

/// <summary>
/// Runs the backfill's real queries against PostgreSQL: which submissions each completion scope
/// pages, the cursor, the deleted-submission lookup, and the skip decision with database timestamps.
/// Unit tests stub these queries, so an inverted filter would pass them.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class SubmissionBackfillScopeIntegrationTests
{
    private const long TenantId = 43;
    private const string DefinitionJson = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1","title":"Question 1"}]}]}""";

    private readonly DbIntegrationFixture _fixture;

    public SubmissionBackfillScopeIntegrationTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PageSpec_CompletedScope_ReturnsOnlyLiveCompletedSubmissionsOfTheForm()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var appDb = CreateAppDbContext();

        // Act
        var page = await CreateSubmissionRepository(appDb).ListAsync(
            new SubmissionBackfillPageSpec(seed.FormId, afterSubmissionId: null, take: 10),
            cancellationToken);

        // Assert
        page.Select(candidate => candidate.SubmissionId)
            .Should().Equal(seed.Completed1, seed.Completed2);
    }

    [Fact]
    public async Task PageSpec_IncompleteScope_ReturnsOnlyLiveDraftsOfTheForm()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var appDb = CreateAppDbContext();

        // Act
        var page = await CreateSubmissionRepository(appDb).ListAsync(
            new SubmissionBackfillPageSpec(seed.FormId, afterSubmissionId: null, take: 10, SubmissionBackfillCompletion.Incomplete),
            cancellationToken);

        // Assert
        page.Select(candidate => candidate.SubmissionId)
            .Should().Equal(seed.Draft1, seed.Draft2);
        page.Should().OnlyContain(candidate => candidate.CreatedAt != default);
    }

    [Fact]
    public async Task PageSpec_PagesInIdOrderAfterTheCursor()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var appDb = CreateAppDbContext();
        var repository = CreateSubmissionRepository(appDb);

        // Act
        var first = await repository.ListAsync(
            new SubmissionBackfillPageSpec(seed.FormId, afterSubmissionId: null, take: 1, SubmissionBackfillCompletion.Incomplete),
            cancellationToken);
        var second = await repository.ListAsync(
            new SubmissionBackfillPageSpec(seed.FormId, first[^1].SubmissionId, take: 1, SubmissionBackfillCompletion.Incomplete),
            cancellationToken);
        var third = await repository.ListAsync(
            new SubmissionBackfillPageSpec(seed.FormId, second[^1].SubmissionId, take: 1, SubmissionBackfillCompletion.Incomplete),
            cancellationToken);

        // Assert
        first.Select(candidate => candidate.SubmissionId).Should().Equal(seed.Draft1);
        second.Select(candidate => candidate.SubmissionId).Should().Equal(seed.Draft2);
        third.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletionStateSpec_FindsSoftDeletedSubmissionPastQueryFilters()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var appDb = CreateAppDbContext();
        var repository = CreateSubmissionRepository(appDb);

        // Act
        var deleted = await repository.SingleOrDefaultAsync(
            new SubmissionDeletionStateSpec(seed.DeletedDraft),
            cancellationToken);
        var missing = await repository.SingleOrDefaultAsync(
            new SubmissionDeletionStateSpec(long.MaxValue),
            cancellationToken);

        // Assert
        deleted.Should().Be(new SubmissionDeletionState(TenantId, seed.FormId, IsDeleted: true));
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Backfill_SkipsAnUnchangedDraft_AndRefreshesItAfterAnEdit()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var processor = await CreateBackfillProcessorAsync(appDb, reportingDb, seed, cancellationToken);
        SubmissionBackfillOptions incomplete = new(Completion: SubmissionBackfillCompletion.Incomplete);

        // Act: first run flattens both drafts; the second finds nothing changed.
        var first = await processor.BackfillFormAsync(TenantId, seed.FormId, incomplete, cancellationToken);
        var unchanged = await processor.BackfillFormAsync(TenantId, seed.FormId, incomplete, cancellationToken);

        // The respondent saves the draft again (API stamps ModifiedAt).
        await appDb.Submissions
            .Where(submission => submission.Id == seed.Draft1)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(submission => submission.JsonData, """{"q1":"edited"}""")
                    .SetProperty(submission => submission.ModifiedAt, DateTime.UtcNow),
                cancellationToken);
        var afterEdit = await processor.BackfillFormAsync(TenantId, seed.FormId, incomplete, cancellationToken);

        // Assert
        first.Processed.Should().Be(2);
        unchanged.Skipped.Should().Be(2, "database timestamps round-trip exactly, so an unchanged draft is not reprocessed");
        afterEdit.Processed.Should().Be(1);
        afterEdit.Skipped.Should().Be(1);

        reportingDb.ChangeTracker.Clear();
        var row = await reportingDb.FlattenedSubmissions.SingleAsync(
            flattened => flattened.SubmissionId == seed.Draft1,
            cancellationToken);
        row.DataJson.Should().Contain("edited");
        row.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);
    }

    [Fact]
    public async Task Backfill_RefreshesADraftSavedWhileItWasBeingFlattened()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var processor = await CreateBackfillProcessorAsync(appDb, reportingDb, seed, cancellationToken);
        SubmissionBackfillOptions incomplete = new(Completion: SubmissionBackfillCompletion.Incomplete);
        await processor.BackfillFormAsync(TenantId, seed.FormId, incomplete, cancellationToken);

        // The draft was saved after the worker read it but before the worker saved the row: its new
        // stamp is older than the row's own ModifiedAt, which the worker set last.
        reportingDb.ChangeTracker.Clear();
        var flattened = await reportingDb.FlattenedSubmissions.SingleAsync(
            row => row.SubmissionId == seed.Draft1,
            cancellationToken);
        var savedDuringFlatten = flattened.SourceModifiedAt!.Value.AddTicks(10);
        savedDuringFlatten.Should().BeBefore(flattened.ModifiedAt!.Value);
        await appDb.Submissions
            .Where(submission => submission.Id == seed.Draft1)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(submission => submission.JsonData, """{"q1":"saved mid-flatten"}""")
                    .SetProperty(submission => submission.ModifiedAt, savedDuringFlatten),
                cancellationToken);

        // Act
        var result = await processor.BackfillFormAsync(TenantId, seed.FormId, incomplete, cancellationToken);

        // Assert
        result.Processed.Should().Be(1);
        reportingDb.ChangeTracker.Clear();
        var refreshed = await reportingDb.FlattenedSubmissions.SingleAsync(
            row => row.SubmissionId == seed.Draft1,
            cancellationToken);
        refreshed.DataJson.Should().Contain("saved mid-flatten");
    }

    [Fact]
    public async Task Backfill_SkipsACompletedSubmissionWhoseRowAFlattenJobWrote()
    {
        // Arrange — the flatten job's path writes the row through the revision-guarded writes.
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var backfill = await CreateBackfillProcessorAsync(appDb, reportingDb, seed, cancellationToken);
        await CreateFlatteningProcessor(appDb, reportingDb)
            .ProcessAsync(TenantId, seed.FormId, seed.Completed1, cancellationToken);

        // Act
        var result = await backfill.BackfillFormAsync(TenantId, seed.FormId, new SubmissionBackfillOptions(), cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        var row = await reportingDb.FlattenedSubmissions.SingleAsync(
            flattened => flattened.SubmissionId == seed.Completed1,
            cancellationToken);
        var submission = await appDb.Submissions.AsNoTracking().SingleAsync(
            source => source.Id == seed.Completed1,
            cancellationToken);
        row.SourceModifiedAt.Should().Be(submission.ModifiedAt ?? submission.CreatedAt);
        row.SourceRevision.Should().Be(submission.Revision);
        result.Skipped.Should().Be(1, "the job's write stored the stamp the backfill compares with");
        result.Processed.Should().Be(1, "only the submission no flatten wrote yet is processed");
    }

    private async Task<SeededForms> SeedAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        await using var appDb = CreateAppDbContext();
        appDb.Set<Tenant>().Add(new Tenant("backfill-scope-tenant", "tnntbfsc") { Id = TenantId });
        await appDb.SaveChangesAsync(cancellationToken);

        (var formId, var definitionId) = await SeedFormAsync(appDb, "Backfill scope form", cancellationToken);
        (var otherFormId, var otherDefinitionId) = await SeedFormAsync(appDb, "Other form", cancellationToken);

        // Interleave kinds so id order alone cannot satisfy a scope.
        var completed1 = await SeedSubmissionAsync(appDb, formId, definitionId, isComplete: true, cancellationToken);
        var draft1 = await SeedSubmissionAsync(appDb, formId, definitionId, isComplete: false, cancellationToken);
        var deletedCompleted = await SeedSubmissionAsync(appDb, formId, definitionId, isComplete: true, cancellationToken);
        var deletedDraft = await SeedSubmissionAsync(appDb, formId, definitionId, isComplete: false, cancellationToken);
        await SeedSubmissionAsync(appDb, otherFormId, otherDefinitionId, isComplete: false, cancellationToken);
        await SeedSubmissionAsync(appDb, otherFormId, otherDefinitionId, isComplete: true, cancellationToken);
        var completed2 = await SeedSubmissionAsync(appDb, formId, definitionId, isComplete: true, cancellationToken);
        var draft2 = await SeedSubmissionAsync(appDb, formId, definitionId, isComplete: false, cancellationToken);

        await appDb.Submissions
            .Where(submission => submission.Id == deletedCompleted || submission.Id == deletedDraft)
            .ExecuteUpdateAsync(updates => updates.SetProperty(submission => submission.IsDeleted, true), cancellationToken);

        return new SeededForms(formId, definitionId, completed1, completed2, draft1, draft2, deletedDraft);
    }

    private static async Task<(long FormId, long DefinitionId)> SeedFormAsync(
        AppDbContext appDb,
        string name,
        CancellationToken cancellationToken)
    {
        // Two-step form+definition save avoids the Form ↔ ActiveDefinition circular insert.
        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: name));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(cancellationToken);

        FormDefinition definition = new(TenantId, isDraft: false, jsonData: DefinitionJson);
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(cancellationToken);
        return (form.Id, definition.Id);
    }

    private static async Task<long> SeedSubmissionAsync(
        AppDbContext appDb,
        long formId,
        long formDefinitionId,
        bool isComplete,
        CancellationToken cancellationToken)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: formId,
            FormDefinitionId: formDefinitionId,
            JsonData: """{"q1":"first"}""",
            IsComplete: isComplete));
        appDb.Submissions.Add(submission);
        await appDb.SaveChangesAsync(cancellationToken);

        // OwnsOne Status uses shared static instances; clear tracking before the next Add.
        appDb.ChangeTracker.Clear();
        return submission.Id;
    }

    private static async Task<SubmissionBackfillProcessor> CreateBackfillProcessorAsync(
        AppDbContext appDb,
        ReportingDbContext reportingDb,
        SeededForms seed,
        CancellationToken cancellationToken)
    {
        FormSchemaRepository schemaRepository = new(reportingDb, new ReportingUnitOfWork(reportingDb));
        var compiled = new FormSchemaCompiler().CompilePersisted(DefinitionJson);
        await schemaRepository.SaveAsync(
            new FormSchema(TenantId, seed.FormId, seed.DefinitionId, compiled.FlatteningMapJson, compiled.CodebookJson),
            cancellationToken);

        return new SubmissionBackfillProcessor(
            CreateSubmissionRepository(appDb),
            new FlattenedSubmissionRepository(reportingDb, new ReportingUnitOfWork(reportingDb)),
            CreateFlatteningProcessor(appDb, reportingDb),
            NullLogger<SubmissionBackfillProcessor>.Instance);
    }

    private static SubmissionFlatteningProcessor CreateFlatteningProcessor(AppDbContext appDb, ReportingDbContext reportingDb) =>
        new(
            CreateSubmissionRepository(appDb),
            new FlattenedSubmissionRepository(reportingDb, new ReportingUnitOfWork(reportingDb)),
            new FormSchemaProvider(
                new FormSchemaRepository(reportingDb, new ReportingUnitOfWork(reportingDb)),
                Substitute.For<IFormSchemaProcessor>()),
            NullLogger<SubmissionFlatteningProcessor>.Instance);

    private static EfRepository<Submission> CreateSubmissionRepository(AppDbContext appDb) =>
        new(appDb, new EndatixSpecificationEvaluator([]));

    private AppDbContext CreateAppDbContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigurePostgreSqlOptions(optionsBuilder, _fixture.ConnectionString);
        return new AppDbContext(
            optionsBuilder.Options,
            new IntegrationTenantContext(TenantId),
            new OutboxIntegrationEventDispatcher());
    }

    private ReportingDbContext CreateReportingDbContext() =>
        new(
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString).Options,
            new IntegrationTenantContext(TenantId));

    private sealed record SeededForms(
        long FormId,
        long DefinitionId,
        long Completed1,
        long Completed2,
        long Draft1,
        long Draft2,
        long DeletedDraft);
}
