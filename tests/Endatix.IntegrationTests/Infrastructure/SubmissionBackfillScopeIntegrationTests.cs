using Endatix.Core.Infrastructure;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static Endatix.IntegrationTests.SubmissionFlatteningWorld;

namespace Endatix.IntegrationTests;

/// <summary>
/// Runs the backfill's real queries against the database: which submissions each completion scope
/// pages, the cursor, and the skip decision with database timestamps.
/// Unit tests stub these queries, so an inverted filter would pass them.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class SubmissionBackfillScopeIntegrationTests(DbIntegrationFixture fixture)
{
    private readonly SubmissionFlatteningWorld _world = new(fixture);

    [Fact]
    public async Task PageSpec_CompletedScope_ReturnsOnlyLiveCompletedSubmissionsOfTheForm()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();

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
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();

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
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();
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
    public async Task Backfill_SkipsAnUnchangedDraft_AndRefreshesItAfterAnEdit()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();
        await using var reportingDb = _world.CreateReportingDbContext();
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
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();
        await using var reportingDb = _world.CreateReportingDbContext();
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
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();
        await using var reportingDb = _world.CreateReportingDbContext();
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

    private static async Task<SubmissionBackfillProcessor> CreateBackfillProcessorAsync(
        AppDbContext appDb,
        ReportingDbContextBase reportingDb,
        SeededForms seed,
        CancellationToken cancellationToken)
    {
        await CreateSchemaRepository(reportingDb).SaveAsync(CompiledSchema(seed), cancellationToken);

        return new SubmissionBackfillProcessor(
            CreateSubmissionRepository(appDb),
            new FlattenedSubmissionRepository(reportingDb, new ReportingUnitOfWork(reportingDb), new DateTimeProvider()),
            CreateFlatteningProcessor(appDb, reportingDb),
            NullLogger<SubmissionBackfillProcessor>.Instance);
    }
}
