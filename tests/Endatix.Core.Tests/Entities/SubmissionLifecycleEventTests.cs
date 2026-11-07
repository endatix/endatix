using Endatix.Core.Entities;
using Endatix.Core.Events;
using FluentAssertions;
using static Endatix.Core.Tests.Entities.SubmissionLifecycleTestKit;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// Every lifecycle change of a submission raises exactly one <see cref="SubmissionLifecycleEvent"/>, named by the phases
/// before and after it, and raises the revision by exactly one. In-progress answer saves on an active submission raise
/// nothing.
/// </summary>
public class SubmissionLifecycleEventTests
{
    [Theory]
    [InlineData(true, CollectionStatusCodes.InProgress, "active")]
    [InlineData(false, CollectionStatusCodes.NotStarted, "pending")]
    public void Create_Open_RaisesOneCreatedEventCarryingTheSavedStatus(bool startSubmission, string expectedCode, string expectedPhase)
    {
        // Act
        var submission = Create(Started with { StartSubmission = startSubmission });

        // Assert
        var created = submission.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SubmissionLifecycleEvent>().Subject;
        created.EventType.Should().Be(SubmissionLifecycleEvent.EventTypes.Created);
        submission.CollectionStatus.Code.Should().Be(expectedCode);
        submission.Revision.Should().Be(2, "creation is one event");
        var payload = (SubmissionLifecyclePayload)created.GetPayload();
        payload.CollectionStatus.Should().Be(new CollectionStatusRef(expectedCode, expectedPhase));
        payload.PreviousCollectionStatus.Should().BeNull();
        payload.Revision.Should().Be(2);
    }

    [Fact]
    public void Create_Complete_RaisesCreatedThenCompletedOnTheirOwnRevisions()
    {
        // Act
        var submission = Create(Started with { IsComplete = true });

        // Assert
        var events = submission.DomainEvents.OfType<SubmissionLifecycleEvent>().ToList();
        events.Select(e => e.EventType).Should().Equal(
            SubmissionLifecycleEvent.EventTypes.Created, SubmissionLifecycleEvent.EventTypes.Completed);
        events.Select(e => e.Revision).Should().Equal(2, 3);
        submission.Revision.Should().Be(3);
        var completed = (SubmissionCompletedPayload)events[1].GetPayload();
        completed.PreviousCollectionStatus.Should().Be(new CollectionStatusRef(CollectionStatusCodes.InProgress, "active"));
        completed.CollectionStatus.Should().Be(new CollectionStatusRef(CollectionStatusCodes.Complete, "completed"));
        completed.IsComplete.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, CollectionStatusCodes.InProgress)]
    [InlineData(false, CollectionStatusCodes.NotStarted)]
    public void Create_WithOutcome_RaisesCreatedInTheOutcomeThenClosed(bool startSubmission, string expectedEntry)
    {
        // Act
        var submission = Create(Started with { StartSubmission = startSubmission, CollectionOutcome = CollectionStatus.ScreenOut });

        // Assert
        var events = submission.DomainEvents.OfType<SubmissionLifecycleEvent>().ToList();
        events.Select(e => e.EventType).Should().Equal(
            SubmissionLifecycleEvent.EventTypes.Created, SubmissionLifecycleEvent.EventTypes.Closed);
        events.Select(e => e.Revision).Should().Equal(2, 3);
        var created = (SubmissionLifecyclePayload)events[0].GetPayload();
        created.CollectionStatus.Code.Should().Be(CollectionStatusCodes.ScreenOut, "created carries the status the row is saved in");
        var closed = (SubmissionLifecyclePayload)events[1].GetPayload();
        closed.PreviousCollectionStatus!.Value.Code.Should().Be(expectedEntry);
        submission.CollectionStatus.Code.Should().Be(CollectionStatusCodes.ScreenOut);
        submission.IsComplete.Should().BeFalse();
        submission.HasStarted.Should().BeTrue();
    }

    [Fact]
    public void Create_WithOutcomeAndComplete_Throws()
    {
        // Act
        var create = () => Create(Started with { IsComplete = true, CollectionOutcome = CollectionStatus.ScreenOut });

        // Assert
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithAnOpenStatusAsOutcome_Throws()
    {
        // Act
        var create = () => Create(Started with { CollectionOutcome = CollectionStatus.InProgress });

        // Assert
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TestSubmission_CreatedPayloadCarriesTheTestFlagAndTenant()
    {
        // Act
        var submission = Create(Started with { IsTestSubmission = true });

        // Assert
        var payload = (SubmissionLifecyclePayload)SingleLifecycleEvent(submission).GetPayload();
        payload.IsTestSubmission.Should().BeTrue();
        payload.TenantId.Should().Be(TenantId);
        payload.FormId.Should().Be(FormId);
        payload.FormDefinitionId.Should().Be(FormDefinitionId);
    }

    [Fact]
    public void Update_InProgressOnPending_RaisesStarted()
    {
        // Arrange
        var submission = CreateCleared(startSubmission: false);
        var revision = submission.Revision;

        // Act
        SaveAnswers(submission, """{"a":1}""");

        // Assert
        AssertSingle(submission, (SubmissionLifecycleEvent.EventTypes.Started, CollectionStatusCodes.NotStarted, CollectionStatusCodes.InProgress));
        submission.Revision.Should().Be(revision + 1);
        submission.HasStarted.Should().BeTrue();
    }

    [Fact]
    public void Update_CompletingOnPending_RaisesOneCompleted()
    {
        // Arrange
        var submission = CreateCleared(startSubmission: false);
        var revision = submission.Revision;

        // Act
        SaveAnswers(submission, """{"a":1}""", isComplete: true);

        // Assert
        AssertSingle(submission, (SubmissionLifecycleEvent.EventTypes.Completed, CollectionStatusCodes.NotStarted, CollectionStatusCodes.Complete));
        submission.Revision.Should().Be(revision + 1);
        submission.StartedAt.Should().Be(submission.CompletedAt);
    }

    [Fact]
    public void Update_InProgressOnActive_RaisesNothingAndKeepsTheRevision()
    {
        // Arrange
        var submission = CreateCleared();
        var revision = submission.Revision;

        // Act
        for (var save = 1; save <= 5; save++)
        {
            SaveAnswers(submission, $$"""{"a":{{save}}}""");
        }

        // Assert
        submission.DomainEvents.Should().BeEmpty();
        submission.Revision.Should().Be(revision);
    }

    [Fact]
    public void Update_CompletingOnActive_RaisesOneCompletedWithTheAnswers()
    {
        // Arrange
        var submission = CreateCleared();
        var revision = submission.Revision;

        // Act
        SaveAnswers(submission, """{"a":1}""", isComplete: true);

        // Assert
        var completed = AssertSingle(submission, (SubmissionLifecycleEvent.EventTypes.Completed, CollectionStatusCodes.InProgress, CollectionStatusCodes.Complete));
        submission.Revision.Should().Be(revision + 1, "one event, one revision");
        var payload = completed.GetPayload().Should().BeOfType<SubmissionCompletedPayload>().Subject;
        payload.JsonData.Should().Be("""{"a":1}""");
        payload.IsComplete.Should().BeTrue();
        payload.CompletedAt.Should().Be(submission.CompletedAt);
    }

    [Fact]
    public void Update_EditOfComplete_RaisesUpdatedAtTheNextRevision()
    {
        // Arrange
        var submission = Create(Started with { IsComplete = true });
        submission.ClearDomainEvents();
        var revision = submission.Revision;

        // Act
        SaveAnswers(submission, """{"a":2}""");

        // Assert
        submission.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SubmissionUpdatedEvent>();
        submission.Revision.Should().Be(revision + 1);
    }

    [Theory]
    [InlineData(true, CollectionStatusCodes.InProgress)]
    [InlineData(false, CollectionStatusCodes.NotStarted)]
    public void ScreenOut_Open_RaisesOneClosedFromTheCurrentStatus(bool startSubmission, string expectedPrevious)
    {
        // Arrange
        var submission = CreateCleared(startSubmission);
        var revision = submission.Revision;

        // Act
        submission.ScreenOut();

        // Assert
        AssertSingle(submission, (SubmissionLifecycleEvent.EventTypes.Closed, expectedPrevious, CollectionStatusCodes.ScreenOut));
        submission.Revision.Should().Be(revision + 1);
        submission.HasStarted.Should().BeTrue();
    }

    [Fact]
    public void ScreenOut_WithAnswersOnPending_RaisesOneClosedAndSavesTheAnswers()
    {
        // Arrange
        var submission = CreateCleared(startSubmission: false);
        var revision = submission.Revision;

        // Act
        submission.ScreenOut("""{"age":"under_18"}""", FormDefinitionId, formDefinitionFormId: FormId);

        // Assert
        AssertSingle(submission, (SubmissionLifecycleEvent.EventTypes.Closed, CollectionStatusCodes.NotStarted, CollectionStatusCodes.ScreenOut));
        submission.Revision.Should().Be(revision + 1);
        submission.JsonData.Should().Be("""{"age":"under_18"}""");
        submission.HasStarted.Should().BeTrue();
    }

    [Fact]
    public void ScreenOut_Repeated_RaisesNothing()
    {
        // Arrange
        var submission = CreateCleared();
        submission.ScreenOut();
        submission.ClearDomainEvents();
        var revision = submission.Revision;

        // Act
        submission.ScreenOut();

        // Assert
        submission.DomainEvents.Should().BeEmpty();
        submission.Revision.Should().Be(revision);
    }

    [Fact]
    public void Close_WithAnotherOutcomeOnClosed_ThrowsAndChangesNothing()
    {
        // Arrange
        var submission = CreateCleared();
        submission.ScreenOut();
        submission.ClearDomainEvents();

        // Act
        var cancel = () => submission.Cancel();

        // Assert
        cancel.Should().Throw<InvalidOperationException>();
        submission.CollectionStatus.Code.Should().Be(CollectionStatusCodes.ScreenOut);
        submission.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Close_OnComplete_Throws()
    {
        // Arrange
        var submission = Create(Started with { IsComplete = true });

        // Act
        var close = () => submission.Close(CollectionStatus.QuotaFull);

        // Assert
        close.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Close_WithAnOpenStatus_Throws()
    {
        // Arrange
        var submission = CreateCleared();

        // Act
        var close = () => submission.Close(CollectionStatus.Expired);

        // Assert
        close.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Close_WithAnswersOnClosed_ThrowsBeforeSavingTheAnswers()
    {
        // Arrange
        var submission = CreateCleared();
        submission.Cancel();

        // Act
        var close = () => submission.Close(CollectionStatus.ScreenOut, new SubmissionContent("""{"a":1}""", FormDefinitionId), FormId);

        // Assert
        close.Should().Throw<InvalidOperationException>();
        submission.JsonData.Should().Be("{}");
    }

    [Fact]
    public void Cancel_Open_RaisesOneClosed()
    {
        // Arrange
        var submission = CreateCleared();
        var revision = submission.Revision;

        // Act
        submission.Cancel();

        // Assert
        AssertSingle(submission, (SubmissionLifecycleEvent.EventTypes.Closed, CollectionStatusCodes.InProgress, CollectionStatusCodes.Cancelled));
        submission.Revision.Should().Be(revision + 1);
    }

    [Fact]
    public void EnsureStarted_OnPendingTwice_RaisesOneStarted()
    {
        // Arrange
        var submission = CreateCleared(startSubmission: false);

        // Act
        submission.EnsureStarted();
        submission.EnsureStarted();

        // Assert
        AssertSingle(submission, (SubmissionLifecycleEvent.EventTypes.Started, CollectionStatusCodes.NotStarted, CollectionStatusCodes.InProgress));
    }

    [Fact]
    public void EnsureStarted_OnLapsed_RaisesNothing()
    {
        // Arrange
        var submission = Loaded(CollectionStatusCodes.Expired);

        // Act
        submission.EnsureStarted();

        // Assert
        submission.DomainEvents.Should().BeEmpty();
        submission.CollectionStatus.Code.Should().Be(CollectionStatusCodes.Expired);
    }

    [Fact]
    public void Revision_AcrossEveryEventKind_RisesByOnePerEvent()
    {
        // Arrange
        var submission = Create(Started with { StartSubmission = false });

        // Act
        submission.EnsureStarted();
        submission.UpdateStatus(SubmissionStatus.FromCode(SubmissionStatusCodes.Read));
        SaveAnswers(submission, """{"a":1}""", isComplete: true);
        submission.MarkAsTest();

        // Assert
        var revisions = submission.DomainEvents.Select(RevisionOf).ToList();
        revisions.Should().Equal(2, 3, 4, 5, 6);
        submission.Revision.Should().Be(6);
    }

    private static long RevisionOf(Core.Infrastructure.Domain.DomainEventBase domainEvent) => domainEvent switch
    {
        SubmissionLifecycleEvent lifecycle => lifecycle.Revision,
        SubmissionUpdatedEvent updated => ((SubmissionUpdatedEvent.Payload)updated.GetPayload()).Revision,
        SubmissionStatusChangedEvent changed => ((SubmissionStatusChangedEvent.Payload)changed.GetPayload()).Revision,
        _ => throw new InvalidOperationException(domainEvent.GetType().Name),
    };
}
