using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FluentAssertions;
using NSubstitute;
using FlattenedSubmissionKey = Endatix.Modules.Reporting.Domain.FlattenedSubmissionKey;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

/// <summary>
/// A submission or its form deleted while it is being flattened, after its deletion sync already ran: the flatten
/// removes the row it wrote, whichever way it ended.
/// </summary>
public partial class SubmissionFlatteningProcessorTests
{
    private const string UnreadableJson = "not json";

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ProcessAsync_SubmissionOrItsFormDeletedAfterRead_RemovesTheRowItWrote(
        bool submissionDeleted,
        bool formDeleted)
    {
        // Arrange — deleted, and its cleanup run, while this flatten was running.
        GivenSubmission(CompletedSubmission(FormSchemaFixtureLoader.LoadText("simple-submission.json"), revision: 2));
        GivenSchema();
        GivenRowAcceptsRevision(true);
        GivenDeletionState(new SubmissionDeletionState(FormId, submissionDeleted, formDeleted));

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert — the row is removed as the deletion's own sync would have removed it.
        await _rows.Received(1).TryMarkProcessedAsync(Arg.Any<FlattenedRevision>(), Arg.Any<FlattenedRowData>(), Arg.Any<CancellationToken>());
        await _rows.Received(1).DeleteBySubmissionAsync(Key, Arg.Any<CancellationToken>());
        await _rows.DidNotReceiveWithAnyArgs().SaveAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_NewerRevisionWrittenMeanwhileThenFormDeleted_RemovesTheRow()
    {
        // Arrange — this flatten's processing write lands, a newer flatten's data replaces it, and the form is
        // deleted, with its cleanup run, before this flatten finishes.
        var submission = CompletedSubmission(FormSchemaFixtureLoader.LoadText("simple-submission.json"), revision: 2);
        GivenSubmission(submission);
        GivenSchema();
        _rows.TryMarkProcessingAsync(WriteOf(submission), Arg.Any<CancellationToken>()).Returns(true);
        _rows.TryMarkProcessedAsync(WriteOf(submission), Arg.Any<FlattenedRowData>(), Arg.Any<CancellationToken>()).Returns(false);
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: true));

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await _rows.Received(1).DeleteBySubmissionAsync(Key, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ProcessAsync_IncompleteSubmissionOrItsFormDeletedAfterRead_RemovesTheSkippedRowItWrote(
        bool submissionDeleted,
        bool formDeleted)
    {
        // Arrange — deleted, and its cleanup run, while this flatten of a draft was running.
        var draft = Submission.Create(new SubmissionCreateArgs(TenantId, FormId, FormDefinitionId, "{}", IsComplete: false));
        draft.Id = SubmissionId;
        GivenSubmission(draft);
        GivenRowAcceptsRevision(true);
        GivenDeletionState(new SubmissionDeletionState(FormId, submissionDeleted, formDeleted));

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert — the skipped row goes too, as the deletion's own sync would have removed it.
        await _rows.Received(1).TryMarkSkippedAsync(WriteOf(draft), Arg.Any<CancellationToken>());
        await _rows.Received(1).DeleteBySubmissionAsync(Key, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_SchemaGoneWithFormDeletedAfterRead_RemovesTheRowItCreatedAndSucceeds()
    {
        // Arrange — the form is deleted, and its cleanup has run, between reading the submission and its schema.
        GivenSubmission(CompletedSubmission("{}", revision: 1));
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: true));
        GivenNoSchema();
        GivenRowAcceptsRevision(true);

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await _rows.Received(1).DeleteBySubmissionAsync(Key, Arg.Any<CancellationToken>());
        await _rows.DidNotReceiveWithAnyArgs().TryMarkProcessedAsync(default, default!, Arg.Any<CancellationToken>());
        await _submissions.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_SkippedWriteLostThenFormDeleted_RemovesTheRow()
    {
        // Arrange — this flatten's processing write lands, a newer flatten's write replaces it before the skip, and
        // the form is deleted, with its cleanup run, before this flatten finishes.
        var draft = Submission.Create(new SubmissionCreateArgs(TenantId, FormId, FormDefinitionId, "{}", IsComplete: false));
        draft.Id = SubmissionId;
        GivenSubmission(draft);
        _rows.TryMarkProcessingAsync(WriteOf(draft), Arg.Any<CancellationToken>()).Returns(true);
        _rows.TryMarkSkippedAsync(WriteOf(draft), Arg.Any<CancellationToken>()).Returns(false);
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: true));

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await _rows.Received(1).DeleteBySubmissionAsync(Key, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ProcessAsync_FlattenFailsAfterProcessingWithSubmissionOrFormDeleted_RemovesTheRowAndSucceeds(
        bool submissionDeleted,
        bool formDeleted)
    {
        // Arrange — deleted, and its cleanup run, after the row was marked processing; then the submission's data
        // cannot be read.
        GivenSubmission(CompletedSubmission(UnreadableJson, revision: 1));
        GivenSchema();
        GivenRowAcceptsRevision(true);
        GivenDeletionState(new SubmissionDeletionState(FormId, submissionDeleted, formDeleted));

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert — nothing is left to retry once the row is gone.
        await act.Should().NotThrowAsync();
        await _rows.Received(1).DeleteBySubmissionAsync(Key, Arg.Any<CancellationToken>());
        await _rows.DidNotReceive().TryMarkProcessedAsync(Arg.Any<FlattenedRevision>(), Arg.Any<FlattenedRowData>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_FlattenFailsAfterProcessingWhileLive_RethrowsTheFailure()
    {
        // Arrange
        GivenSubmission(CompletedSubmission(UnreadableJson, revision: 1));
        GivenSchema();
        GivenRowAcceptsRevision(true);
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: false));

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<JsonException>();
        await _submissions.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>());
        await _rows.DidNotReceive().DeleteBySubmissionAsync(Arg.Any<FlattenedSubmissionKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_DeletionCheckFailsAfterFlattenFailed_RethrowsTheFlattenFailure()
    {
        // Arrange
        GivenSubmission(CompletedSubmission(UnreadableJson, revision: 1));
        GivenSchema();
        GivenRowAcceptsRevision(true);
        _submissions.SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SubmissionDeletionState?>(new InvalidOperationException("database unavailable")));

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<JsonException>();
        await _rows.DidNotReceive().DeleteBySubmissionAsync(Arg.Any<FlattenedSubmissionKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_SkippedWriteFailsWithFormDeleted_RemovesTheRowAndSucceeds()
    {
        // Arrange — the form is deleted, and its cleanup run, after the draft's row was marked processing; then the
        // skip itself fails.
        var draft = Submission.Create(new SubmissionCreateArgs(TenantId, FormId, FormDefinitionId, "{}", IsComplete: false));
        draft.Id = SubmissionId;
        GivenSubmission(draft);
        _rows.TryMarkProcessingAsync(WriteOf(draft), Arg.Any<CancellationToken>()).Returns(true);
        _rows.TryMarkSkippedAsync(WriteOf(draft), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("write failed")));
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: true));

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        await _rows.Received(1).DeleteBySubmissionAsync(Key, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RowRemovalFailsAfterFlattenFailed_RethrowsTheFlattenFailure()
    {
        // Arrange — the lookup finds the form deleted, but removing the row fails.
        GivenSubmission(CompletedSubmission(UnreadableJson, revision: 1));
        GivenSchema();
        GivenRowAcceptsRevision(true);
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: true));
        _rows.DeleteBySubmissionAsync(Key, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("database unavailable")));

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task ProcessAsync_CancelledAfterProcessingWithFormDeleted_RemovesTheRowAndStaysCancelled()
    {
        // Arrange — the form was deleted, and its cleanup run, when the flatten is cancelled reading the schema.
        using var cancellation = new CancellationTokenSource();
        GivenCancelledWhileReadingTheSchema(cancellation);
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: true));

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, cancellation.Token);

        // Assert — the check runs on a token of its own, and the cancellation is what the caller sees.
        await act.Should().ThrowAsync<OperationCanceledException>();
        await _submissions.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>());
        await _rows.Received(1).DeleteBySubmissionAsync(Key, Arg.Is<CancellationToken>(token => token != cancellation.Token));
    }

    [Fact]
    public async Task ProcessAsync_CancelledAfterProcessingWhileLive_StaysCancelledWithoutRemovingTheRow()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        GivenCancelledWhileReadingTheSchema(cancellation);
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: false));

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await _submissions.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>());
        await _rows.DidNotReceive().DeleteBySubmissionAsync(Arg.Any<FlattenedSubmissionKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_CancelledAfterTheLastWrite_StillRemovesTheRowOfADeletedForm()
    {
        // Arrange — the draft's skip lands, then the flatten is cancelled before its deletion check.
        using var cancellation = new CancellationTokenSource();
        var draft = Submission.Create(new SubmissionCreateArgs(TenantId, FormId, FormDefinitionId, "{}", IsComplete: false));
        draft.Id = SubmissionId;
        GivenSubmission(draft);
        _rows.TryMarkProcessingAsync(WriteOf(draft), Arg.Any<CancellationToken>()).Returns(true);
        _rows.TryMarkSkippedAsync(WriteOf(draft), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cancellation.Cancel();
            return true;
        });
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: true));

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, cancellation.Token);

        // Assert
        await _rows.Received(1).DeleteBySubmissionAsync(Key, Arg.Is<CancellationToken>(token => token != cancellation.Token));
    }

    private void GivenCancelledWhileReadingTheSchema(CancellationTokenSource cancellation)
    {
        GivenSubmission(CompletedSubmission("{}", revision: 1));
        GivenRowAcceptsRevision(true);
        _schemas.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromException<FormSchemaEntity?>(new OperationCanceledException(cancellation.Token));
            });
    }
}
