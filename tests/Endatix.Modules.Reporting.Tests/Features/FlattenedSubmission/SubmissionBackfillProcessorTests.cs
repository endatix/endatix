using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FlattenedSubmissionKey = Endatix.Modules.Reporting.Domain.FlattenedSubmissionKey;
using FlattenedSubmissionRow = Endatix.Modules.Reporting.Domain.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

public sealed class SubmissionBackfillProcessorTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    /// <summary>The submission's stamp for the version that was flattened.</summary>
    private static readonly DateTime _flattenedVersionAt = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BackfillFormAsync_WithAlreadyProcessedRow_SkipsWithoutFlattening()
    {
        const long submissionId = 10;
        var existing = FlattenedRows.Processed(
            new FlattenedSubmissionKey(TenantId, FormId, submissionId),
            """{"q1":"a"}""",
            _flattenedVersionAt);

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), Arg.Any<CancellationToken>())
            .Returns([Candidate(submissionId)]);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(TenantId, submissionId, Arg.Any<CancellationToken>())
            .Returns(existing);

        var flatteningProcessor = AcceptingProcessor();
        var processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            TestContext.Current.CancellationToken);

        result.Scanned.Should().Be(1);
        result.Skipped.Should().Be(1);
        result.Processed.Should().Be(0);
        result.HasMore.Should().BeFalse();
        await flatteningProcessor.DidNotReceive()
            .TryBackfillAsync(Arg.Any<FlattenedSubmissionKey>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillFormAsync_WithForce_ReprocessesProcessedRow()
    {
        const long submissionId = 10;
        var existing = FlattenedRows.Processed(
            new FlattenedSubmissionKey(TenantId, FormId, submissionId),
            """{"q1":"a"}""",
            _flattenedVersionAt);

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), Arg.Any<CancellationToken>())
            .Returns([Candidate(submissionId)]);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(TenantId, submissionId, Arg.Any<CancellationToken>())
            .Returns(existing);

        var flatteningProcessor = AcceptingProcessor();
        var processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(Force: true),
            TestContext.Current.CancellationToken);

        result.Processed.Should().Be(1);
        result.Skipped.Should().Be(0);
        await flatteningProcessor.Received(1)
            .TryBackfillAsync(new(TenantId, FormId, submissionId), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillFormAsync_WhenFlatteningFails_ContinuesBatchAndReportsFailure()
    {
        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), Arg.Any<CancellationToken>())
            .Returns([Candidate(10), Candidate(11)]);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((FlattenedSubmissionRow?)null);

        var flatteningProcessor = AcceptingProcessor();
        flatteningProcessor
            .TryBackfillAsync(new(TenantId, FormId, 10), false, Arg.Any<CancellationToken>())
            .Returns(true);
        flatteningProcessor
            .TryBackfillAsync(new(TenantId, FormId, 11), false, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("flatten failed")));

        var processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(BatchSize: 2),
            TestContext.Current.CancellationToken);

        result.Scanned.Should().Be(2);
        result.Processed.Should().Be(1);
        result.Failed.Should().Be(1);
        result.FailedSubmissionIds.Should().ContainSingle().Which.Should().Be(11);
    }

    [Fact]
    public async Task BackfillFormAsync_WhenCancellationRequestedDuringFlattening_RethrowsWithoutRecordingFailure()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        var cancellationToken = cancellationTokenSource.Token;

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), Arg.Any<CancellationToken>())
            .Returns([Candidate(10)]);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((FlattenedSubmissionRow?)null);

        var flatteningProcessor = AcceptingProcessor();
        flatteningProcessor
            .TryBackfillAsync(new(TenantId, FormId, 10), false, Arg.Any<CancellationToken>())
            .Returns<bool>(_ =>
            {
                cancellationTokenSource.Cancel();
                throw new TaskCanceledException();
            });

        var processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        Func<Task> act = () => processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            cancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BackfillFormAsync_WhenMoreRowsExist_ReturnsHasMoreAndCursor()
    {
        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), Arg.Any<CancellationToken>())
            .Returns([Candidate(1), Candidate(2), Candidate(3)]);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        var flatteningProcessor = AcceptingProcessor();
        var processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(BatchSize: 2),
            TestContext.Current.CancellationToken);

        result.Scanned.Should().Be(2);
        result.HasMore.Should().BeTrue();
        result.NextAfterSubmissionId.Should().Be(2);
    }

    [Fact]
    public async Task BackfillFormAsync_WhenSubmissionIsNewerThanFlatten_Reprocesses()
    {
        const long submissionId = 10;
        var existing = FlattenedRows.Processed(
            new FlattenedSubmissionKey(TenantId, FormId, submissionId),
            """{"q1":"draft"}""",
            _flattenedVersionAt);

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), Arg.Any<CancellationToken>())
            .Returns([Candidate(submissionId, modifiedAt: _flattenedVersionAt.AddSeconds(1))]);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(TenantId, submissionId, Arg.Any<CancellationToken>())
            .Returns(existing);

        var flatteningProcessor = AcceptingProcessor();
        var processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            TestContext.Current.CancellationToken);

        result.Processed.Should().Be(1);
        result.Skipped.Should().Be(0);
        await flatteningProcessor.Received(1)
            .TryBackfillAsync(new(TenantId, FormId, submissionId), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillFormAsync_WithIncompleteScope_PassesIncludeIncomplete()
    {
        const long submissionId = 10;

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), Arg.Any<CancellationToken>())
            .Returns([Candidate(submissionId)]);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(TenantId, submissionId, Arg.Any<CancellationToken>())
            .Returns((FlattenedSubmissionRow?)null);

        var flatteningProcessor = AcceptingProcessor();
        var processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(Completion: SubmissionBackfillCompletion.Incomplete),
            TestContext.Current.CancellationToken);

        await flatteningProcessor.Received(1)
            .TryBackfillAsync(new(TenantId, FormId, submissionId), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillFormAsync_WhenSavedDuringFlattening_ReprocessesDespiteNewerRowStamp()
    {
        // Arrange: the draft was saved after the worker read it, but before the worker saved the
        // row, so the row's own ModifiedAt (worker clock, set last) is newer than the submission.
        const long submissionId = 10;
        var existing = FlattenedRows.Processed(
            new FlattenedSubmissionKey(TenantId, FormId, submissionId),
            """{"q1":"old draft"}""",
            _flattenedVersionAt);
        var savedDuringFlatten = _flattenedVersionAt.AddMilliseconds(200);
        existing.ModifiedAt.Should().BeAfter(savedDuringFlatten);

        (var processor, var flatteningProcessor) =
            CreateProcessorFor(existing, Candidate(submissionId, modifiedAt: savedDuringFlatten));

        // Act
        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(Completion: SubmissionBackfillCompletion.Incomplete),
            TestContext.Current.CancellationToken);

        // Assert
        result.Processed.Should().Be(1);
        result.Skipped.Should().Be(0);
        await flatteningProcessor.Received(1)
            .TryBackfillAsync(new(TenantId, FormId, submissionId), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillFormAsync_WhenSubmissionUnchangedSinceFlatten_Skips()
    {
        // Arrange
        const long submissionId = 10;
        var existing = FlattenedRows.Processed(
            new FlattenedSubmissionKey(TenantId, FormId, submissionId),
            """{"q1":"a"}""",
            _flattenedVersionAt);

        (var processor, var flatteningProcessor) =
            CreateProcessorFor(existing, Candidate(submissionId, modifiedAt: _flattenedVersionAt));

        // Act
        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Skipped.Should().Be(1);
        await flatteningProcessor.DidNotReceive()
            .TryBackfillAsync(Arg.Any<FlattenedSubmissionKey>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillFormAsync_WhenRowPredatesSourceStamp_Reprocesses()
    {
        // Arrange: a row processed before SourceModifiedAt existed has no source stamp to compare.
        const long submissionId = 10;
        var existing = FlattenedRows.Processed(
            new FlattenedSubmissionKey(TenantId, FormId, submissionId),
            """{"q1":"a"}""",
            _flattenedVersionAt);
        typeof(FlattenedSubmissionRow)
            .GetProperty(nameof(FlattenedSubmissionRow.SourceModifiedAt))!
            .SetValue(existing, null);

        (var processor, var flatteningProcessor) =
            CreateProcessorFor(existing, Candidate(submissionId));

        // Act
        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Processed.Should().Be(1);
        await flatteningProcessor.Received(1)
            .TryBackfillAsync(new(TenantId, FormId, submissionId), false, Arg.Any<CancellationToken>());
    }

    private static ISubmissionFlatteningProcessor AcceptingProcessor()
    {
        var processor = Substitute.For<ISubmissionFlatteningProcessor>();
        processor.TryBackfillAsync(default, default, default).ReturnsForAnyArgs(true);
        return processor;
    }

    [Fact]
    public async Task BackfillFormAsync_EligibleNonForcedCandidateSuperseded_CountsOnlyASkip()
    {
        FlattenedSubmissionKey key = new(TenantId, FormId, 10);
        var (backfill, writer) = CreateProcessorFor(new FlattenedSubmissionRow(key, _flattenedVersionAt), Candidate(key.SubmissionId));
        var ct = TestContext.Current.CancellationToken;
        writer.TryBackfillAsync(key, false, ct).Returns(false);

        var page = await backfill.BackfillFormAsync(TenantId, FormId, new(), ct);

        await writer.Received(1).TryBackfillAsync(key, false, ct);
        (page.Scanned, page.Skipped, page.Processed, page.Failed).Should().Be((1, 1, 0, 0));
        page.FailedSubmissionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task BackfillFormAsync_ForcedCandidateConfirmedDeletedInThisForm_SkipsWithoutRetry()
    {
        var ct = TestContext.Current.CancellationToken;
        var sources = Substitute.For<IRepository<Submission>>();
        sources.ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), ct).Returns([Candidate(10)]);
        sources.SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), ct)
            .Returns(new SubmissionDeletionState(FormId, IsDeleted: true, FormIsDeleted: false));
        var writer = Substitute.For<ISubmissionFlatteningProcessor>();
        writer.TryBackfillAsync(new(TenantId, FormId, 10), false, ct).Returns(false);

        var page = await CreateProcessor(sources, Substitute.For<IFlattenedSubmissionRepository>(), writer)
            .BackfillFormAsync(TenantId, FormId, new(Force: true), ct);

        (page.Scanned, page.Skipped, page.Processed, page.Failed).Should().Be((1, 1, 0, 0));
        page.FailedSubmissionIds.Should().BeEmpty();
    }

    private static SubmissionBackfillCandidate Candidate(long submissionId, DateTime? modifiedAt = null) =>
        new(submissionId, modifiedAt, CreatedAt: _flattenedVersionAt.AddDays(-1));

    private static (SubmissionBackfillProcessor Processor, ISubmissionFlatteningProcessor FlatteningProcessor)
        CreateProcessorFor(FlattenedSubmissionRow existing, SubmissionBackfillCandidate candidate)
    {
        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), Arg.Any<CancellationToken>())
            .Returns([candidate]);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(TenantId, candidate.SubmissionId, Arg.Any<CancellationToken>())
            .Returns(existing);

        var flatteningProcessor = AcceptingProcessor();
        return (CreateProcessor(submissionRepository, flattenedSubmissionRepository, flatteningProcessor), flatteningProcessor);
    }

    private static SubmissionBackfillProcessor CreateProcessor(
        IRepository<Submission> submissionRepository,
        IFlattenedSubmissionRepository flattenedSubmissionRepository,
        ISubmissionFlatteningProcessor flatteningProcessor) =>
        new(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor,
            NullLogger<SubmissionBackfillProcessor>.Instance);
}
