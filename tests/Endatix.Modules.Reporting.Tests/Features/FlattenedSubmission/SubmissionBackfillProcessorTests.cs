using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FlattenedSubmissionRow = Endatix.Modules.Reporting.Domain.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

public sealed class SubmissionBackfillProcessorTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    [Fact]
    public async Task BackfillFormAsync_WithAlreadyProcessedRow_SkipsWithoutFlattening()
    {
        const long submissionId = 10;
        FlattenedSubmissionRow existing = new(submissionId, TenantId, FormId);
        existing.MarkProcessed("""{"q1":"a"}""");

        IRepository<Submission> submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<CompletedSubmissionIdsForBackfillSpec>(), Arg.Any<CancellationToken>())
            .Returns([submissionId]);

        IFlattenedSubmissionRepository flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(TenantId, submissionId, Arg.Any<CancellationToken>())
            .Returns(existing);

        ISubmissionFlatteningProcessor flatteningProcessor = Substitute.For<ISubmissionFlatteningProcessor>();
        SubmissionBackfillProcessor processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        SubmissionBackfillResult result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            TestContext.Current.CancellationToken);

        result.Scanned.Should().Be(1);
        result.Skipped.Should().Be(1);
        result.Processed.Should().Be(0);
        result.HasMore.Should().BeFalse();
        await flatteningProcessor.DidNotReceive()
            .ProcessAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillFormAsync_WithForce_ReprocessesProcessedRow()
    {
        const long submissionId = 10;
        FlattenedSubmissionRow existing = new(submissionId, TenantId, FormId);
        existing.MarkProcessed("""{"q1":"a"}""");

        IRepository<Submission> submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<CompletedSubmissionIdsForBackfillSpec>(), Arg.Any<CancellationToken>())
            .Returns([submissionId]);

        IFlattenedSubmissionRepository flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(TenantId, submissionId, Arg.Any<CancellationToken>())
            .Returns(existing);

        ISubmissionFlatteningProcessor flatteningProcessor = Substitute.For<ISubmissionFlatteningProcessor>();
        SubmissionBackfillProcessor processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        SubmissionBackfillResult result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(Force: true),
            TestContext.Current.CancellationToken);

        result.Processed.Should().Be(1);
        result.Skipped.Should().Be(0);
        await flatteningProcessor.Received(1)
            .ProcessAsync(TenantId, FormId, submissionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillFormAsync_WhenFlatteningFails_ContinuesBatchAndReportsFailure()
    {
        IRepository<Submission> submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<CompletedSubmissionIdsForBackfillSpec>(), Arg.Any<CancellationToken>())
            .Returns([10L, 11L]);

        IFlattenedSubmissionRepository flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((FlattenedSubmissionRow?)null);

        ISubmissionFlatteningProcessor flatteningProcessor = Substitute.For<ISubmissionFlatteningProcessor>();
        flatteningProcessor
            .ProcessAsync(TenantId, FormId, 10, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        flatteningProcessor
            .ProcessAsync(TenantId, FormId, 11, Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("flatten failed"));

        SubmissionBackfillProcessor processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        SubmissionBackfillResult result = await processor.BackfillFormAsync(
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
    public async Task BackfillFormAsync_WhenMoreRowsExist_ReturnsHasMoreAndCursor()
    {
        IRepository<Submission> submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<CompletedSubmissionIdsForBackfillSpec>(), Arg.Any<CancellationToken>())
            .Returns([1L, 2L, 3L]);

        IFlattenedSubmissionRepository flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        ISubmissionFlatteningProcessor flatteningProcessor = Substitute.For<ISubmissionFlatteningProcessor>();
        SubmissionBackfillProcessor processor = CreateProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor);

        SubmissionBackfillResult result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(BatchSize: 2),
            TestContext.Current.CancellationToken);

        result.Scanned.Should().Be(2);
        result.HasMore.Should().BeTrue();
        result.NextAfterSubmissionId.Should().Be(2);
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
