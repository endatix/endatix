using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using static Endatix.IntegrationTests.SubmissionFlatteningWorld;

namespace Endatix.IntegrationTests;

/// <summary>
/// Runs the flatten's deletion handling against the database: the deleted-submission lookup past the query filters,
/// and a form deleted while a flatten runs. Unit tests stub the lookup, so a filter it misses would pass them.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class FlattenedSubmissionDeletionIntegrationTests(DbIntegrationFixture fixture)
{
    private readonly SubmissionFlatteningWorld _world = new(fixture);

    [Fact]
    public async Task DeletionStateSpec_FindsSoftDeletedSubmissionPastQueryFilters()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();
        var repository = CreateSubmissionRepository(appDb);

        // Act
        var deleted = await repository.SingleOrDefaultAsync(
            new SubmissionDeletionStateSpec(TenantId, seed.DeletedDraft),
            cancellationToken);
        var missing = await repository.SingleOrDefaultAsync(
            new SubmissionDeletionStateSpec(TenantId, long.MaxValue),
            cancellationToken);

        // Assert
        deleted.Should().Be(new SubmissionDeletionState(seed.FormId, IsDeleted: true, FormIsDeleted: false));
        missing.Should().BeNull();
    }

    [Fact]
    public async Task DeletionStateSpec_WithOtherTenant_ReturnsNull()
    {
        // Arrange — the query filters are off for this read, so only its own tenant predicate keeps the row out.
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();

        // Act
        var state = await CreateSubmissionRepository(appDb).SingleOrDefaultAsync(
            new SubmissionDeletionStateSpec(TenantId + 1, seed.DeletedDraft),
            cancellationToken);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task DeletionStateSpec_LiveSubmissionOfDeletedForm_ReportsTheFormDeleted()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();
        await SoftDeleteFormAsync(appDb, seed.FormId, cancellationToken);

        // Act
        var state = await CreateSubmissionRepository(appDb).SingleOrDefaultAsync(
            new SubmissionDeletionStateSpec(TenantId, seed.Completed1),
            cancellationToken);

        // Assert
        state.Should().Be(new SubmissionDeletionState(seed.FormId, IsDeleted: false, FormIsDeleted: true));
    }

    [Fact]
    public async Task Flatten_FormDeletedWhileFlattening_RemovesTheRowItWrote()
    {
        // Arrange — only the form is soft-deleted, after the submission was read and before its row is written; the
        // submission and its definition stay live, so the form's own deletion is all there is to see.
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();
        await using var reportingDb = _world.CreateReportingDbContext();
        var schemas = Substitute.For<IFormSchemaProvider>();
        schemas.GetOrCompileAsync(TenantId, seed.FormId, seed.DefinitionId, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await SoftDeleteFormAsync(appDb, seed.FormId, cancellationToken);
                return (Endatix.Modules.Reporting.Domain.FormSchema?)CompiledSchema(seed);
            });
        var processor = CreateFlatteningProcessor(CreateSubmissionRepository(appDb), reportingDb, schemas);

        // Act
        await processor.ProcessAsync(TenantId, seed.FormId, seed.Completed1, cancellationToken);

        // Assert
        (await RowExistsAsync(reportingDb, seed.Completed1, cancellationToken))
            .Should().BeFalse("the form's deletion sync may already have run, so nothing else would remove the row");
    }

    [Fact]
    public async Task Flatten_DraftOfLiveForm_LeavesASkippedRow()
    {
        // Arrange — the draft of the test below, with nothing deleted: the control that its flatten writes a row.
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();
        await using var reportingDb = _world.CreateReportingDbContext();
        var processor = CreateFlatteningProcessor(
            CreateSubmissionRepository(appDb),
            reportingDb,
            Substitute.For<IFormSchemaProvider>());

        // Act
        await processor.ProcessAsync(TenantId, seed.FormId, seed.Draft1, cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        var row = await reportingDb.FlattenedSubmissions
            .SingleAsync(candidate => candidate.SubmissionId == seed.Draft1, cancellationToken);
        row.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Skipped);
    }

    [Fact]
    public async Task Flatten_DraftWhoseFormIsDeletedWhileFlattening_RemovesTheSkippedRowItWrote()
    {
        // Arrange — only the form is soft-deleted, right after the draft was read and before its row is written.
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _world.SeedAsync(cancellationToken);
        await using var appDb = _world.CreateAppDbContext();
        await using var reportingDb = _world.CreateReportingDbContext();
        var processor = CreateFlatteningProcessor(
            DeletingFormAfterRead(appDb, seed.FormId, cancellationToken),
            reportingDb,
            Substitute.For<IFormSchemaProvider>());

        // Act
        await processor.ProcessAsync(TenantId, seed.FormId, seed.Draft1, cancellationToken);

        // Assert
        (await RowExistsAsync(reportingDb, seed.Draft1, cancellationToken))
            .Should().BeFalse("the form's deletion sync may already have run, so nothing else would remove the row");
    }

    // The submissions as the flatten sees them, with the form soft-deleted between reading the submission and
    // writing its row. It answers only the two calls the processor makes: the read and the deletion-state lookup.
    private static IRepository<Submission> DeletingFormAfterRead(
        AppDbContext appDb,
        long formId,
        CancellationToken cancellationToken)
    {
        var submissions = CreateSubmissionRepository(appDb);
        var deletingForm = Substitute.For<IRepository<Submission>>();
        deletingForm.SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var read = await submissions.SingleOrDefaultAsync(
                    call.Arg<SubmissionWithDefinitionAndFormSpec>(),
                    cancellationToken);
                await SoftDeleteFormAsync(appDb, formId, cancellationToken);
                return read;
            });
        deletingForm.SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => submissions.SingleOrDefaultAsync(call.Arg<SubmissionDeletionStateSpec>(), cancellationToken));
        return deletingForm;
    }

    private static Task<bool> RowExistsAsync(
        ReportingDbContextBase reportingDb,
        long submissionId,
        CancellationToken cancellationToken)
    {
        reportingDb.ChangeTracker.Clear();
        return reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .AnyAsync(row => row.SubmissionId == submissionId, cancellationToken);
    }
}
