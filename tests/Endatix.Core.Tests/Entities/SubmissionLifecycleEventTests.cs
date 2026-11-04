using System.Reflection;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// Every lifecycle change of a submission raises exactly one <see cref="SubmissionLifecycleEvent"/>, named by the phases
/// before and after it, and raises the revision by exactly one. In-progress answer saves on an active submission raise
/// nothing.
/// </summary>
public class SubmissionLifecycleEventTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;

    private static Submission Create(
        bool isComplete = false,
        bool startSubmission = true,
        bool isTest = false,
        CollectionStatus? outcome = null) =>
        Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: FormDefinitionId,
            JsonData: "{}",
            IsComplete: isComplete,
            IsTestSubmission: isTest,
            StartSubmission: startSubmission,
            CollectionOutcome: outcome));

    private static Submission CreateCleared(bool startSubmission = true)
    {
        var submission = Create(startSubmission: startSubmission);
        submission.ClearDomainEvents();
        return submission;
    }

    /// <summary>A submission as it loads from a row stored in <paramref name="code"/>.</summary>
    private static Submission Loaded(string code, bool startSubmission = true)
    {
        var submission = CreateCleared(startSubmission);
        typeof(Submission).GetProperty(nameof(Submission.CollectionStatus), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(submission, CollectionStatus.FromCode(code));
        return submission;
    }

    private static void SaveAnswers(Submission submission, string jsonData, bool isComplete = false) =>
        submission.Update(jsonData, FormDefinitionId, formDefinitionFormId: FormId, isComplete: isComplete);

    private static SubmissionLifecycleEvent SingleLifecycleEvent(Submission submission) =>
        submission.DomainEvents.OfType<SubmissionLifecycleEvent>().Should().ContainSingle().Subject;

    [Theory]
    [InlineData(true, CollectionStatusCodes.InProgress, "active")]
    [InlineData(false, CollectionStatusCodes.NotStarted, "pending")]
    public void Create_Open_RaisesOneCreatedEventCarryingTheSavedStatus(bool startSubmission, string expectedCode, string expectedPhase)
    {
        // Act
        var submission = Create(startSubmission: startSubmission);

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
        var submission = Create(isComplete: true);

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
        var submission = Create(startSubmission: startSubmission, outcome: CollectionStatus.ScreenOut);

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
        var create = () => Create(isComplete: true, outcome: CollectionStatus.ScreenOut);

        // Assert
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithAnOpenStatusAsOutcome_Throws()
    {
        // Act
        var create = () => Create(outcome: CollectionStatus.InProgress);

        // Assert
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TestSubmission_CreatedPayloadCarriesTheTestFlagAndTenant()
    {
        // Act
        var submission = Create(isTest: true);

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
        AssertSingle(submission, SubmissionLifecycleEvent.EventTypes.Started, CollectionStatusCodes.NotStarted, CollectionStatusCodes.InProgress);
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
        AssertSingle(submission, SubmissionLifecycleEvent.EventTypes.Completed, CollectionStatusCodes.NotStarted, CollectionStatusCodes.Complete);
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
        var completed = AssertSingle(submission, SubmissionLifecycleEvent.EventTypes.Completed, CollectionStatusCodes.InProgress, CollectionStatusCodes.Complete);
        submission.Revision.Should().Be(revision + 1, "one event, one revision");
        var payload = completed.GetPayload().Should().BeOfType<SubmissionCompletedPayload>().Subject;
        payload.JsonData.Should().Be("""{"a":1}""");
        payload.IsComplete.Should().BeTrue();
        payload.CompletedAt.Should().Be(submission.CompletedAt);
    }

    [Fact]
    public void Update_CompletingADriftedRow_RaisesCompletedAtItsOwnRevision()
    {
        // Arrange
        var submission = Loaded(CollectionStatusCodes.Complete);
        var revision = submission.Revision;

        // Act
        SaveAnswers(submission, """{"a":1}""", isComplete: true);

        // Assert
        SingleLifecycleEvent(submission).EventType.Should().Be(SubmissionLifecycleEvent.EventTypes.Completed);
        submission.Revision.Should().Be(revision + 1);
        submission.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Update_InProgressOnLapsed_RaisesResumed()
    {
        // Arrange
        var submission = Loaded(CollectionStatusCodes.Expired);

        // Act
        SaveAnswers(submission, """{"a":1}""");

        // Assert
        AssertSingle(submission, SubmissionLifecycleEvent.EventTypes.Resumed, CollectionStatusCodes.Expired, CollectionStatusCodes.InProgress);
    }

    [Theory]
    [InlineData(CollectionStatusCodes.ScreenOut)]
    [InlineData(CollectionStatusCodes.Cancelled)]
    [InlineData("unregistered_code")]
    public void Update_OnClosed_ThrowsAndChangesNothing(string code)
    {
        // Arrange
        var submission = Loaded(code);
        var revision = submission.Revision;

        // Act
        var save = () => SaveAnswers(submission, """{"a":1}""");

        // Assert
        save.Should().Throw<InvalidOperationException>();
        submission.JsonData.Should().Be("{}");
        submission.Revision.Should().Be(revision);
        submission.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_EditOfComplete_RaisesUpdatedAtTheNextRevision()
    {
        // Arrange
        var submission = Create(isComplete: true);
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
        AssertSingle(submission, SubmissionLifecycleEvent.EventTypes.Closed, expectedPrevious, CollectionStatusCodes.ScreenOut);
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
        AssertSingle(submission, SubmissionLifecycleEvent.EventTypes.Closed, CollectionStatusCodes.NotStarted, CollectionStatusCodes.ScreenOut);
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
        var submission = Create(isComplete: true);

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
        var close = () => submission.Close(CollectionStatus.ScreenOut, """{"a":1}""", FormDefinitionId, FormId);

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
        AssertSingle(submission, SubmissionLifecycleEvent.EventTypes.Closed, CollectionStatusCodes.InProgress, CollectionStatusCodes.Cancelled);
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
        AssertSingle(submission, SubmissionLifecycleEvent.EventTypes.Started, CollectionStatusCodes.NotStarted, CollectionStatusCodes.InProgress);
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
        var submission = Create(startSubmission: false);

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

    [Fact]
    public void IncrementRevision_ThroughTheInterface_IsNotSupported()
    {
        // Arrange
        Core.Abstractions.IHasRevision submission = CreateCleared();

        // Act
        var increment = () => submission.IncrementRevision();

        // Assert
        increment.Should().Throw<NotSupportedException>();
    }

    private static long RevisionOf(Core.Infrastructure.Domain.DomainEventBase domainEvent) => domainEvent switch
    {
        SubmissionLifecycleEvent lifecycle => lifecycle.Revision,
        SubmissionUpdatedEvent updated => ((SubmissionUpdatedEvent.Payload)updated.GetPayload()).Revision,
        SubmissionStatusChangedEvent changed => ((SubmissionStatusChangedEvent.Payload)changed.GetPayload()).Revision,
        _ => throw new InvalidOperationException(domainEvent.GetType().Name),
    };

    private static SubmissionLifecycleEvent AssertSingle(Submission submission, string eventType, string previous, string next)
    {
        var lifecycle = SingleLifecycleEvent(submission);
        lifecycle.EventType.Should().Be(eventType);
        var payload = (SubmissionLifecyclePayload)lifecycle.GetPayload();
        payload.CollectionStatus.Code.Should().Be(next);
        payload.PreviousCollectionStatus!.Value.Code.Should().Be(previous);
        payload.Revision.Should().Be(submission.Revision);
        submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().BeEmpty();
        return lifecycle;
    }
}
