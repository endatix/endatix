using Endatix.Core.Entities;
using Endatix.Core.Events;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// A submission raises <c>submission.created</c> once when it is created, and
/// <c>submission.collection_status_changed</c> once per collection status change, each with one revision bump.
/// In-progress answer saves raise nothing.
/// </summary>
public class SubmissionLifecycleEventTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;

    private static Submission Create(bool isComplete = false, bool startSubmission = true, bool isTest = false) =>
        Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: FormDefinitionId,
            JsonData: "{}",
            IsComplete: isComplete,
            IsTestSubmission: isTest,
            StartSubmission: startSubmission));

    private static Submission CreateCleared(bool startSubmission = true)
    {
        var submission = Create(startSubmission: startSubmission);
        submission.ClearDomainEvents();
        return submission;
    }

    private static void SaveAnswers(Submission submission, string jsonData, bool isComplete = false) =>
        submission.Update(jsonData, FormDefinitionId, formDefinitionFormId: FormId, isComplete: isComplete);

    [Theory]
    [InlineData(true, CollectionStatusCodes.InProgress)]
    [InlineData(false, CollectionStatusCodes.NotStarted)]
    public void Create_RaisesOneCreatedEventCarryingTheInitialStatus(bool startSubmission, string expectedStatus)
    {
        // Act
        var submission = Create(startSubmission: startSubmission);

        // Assert
        var created = submission.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SubmissionCreatedEvent>().Subject;
        submission.CollectionStatus.Code.Should().Be(expectedStatus);
        submission.Revision.Should().Be(2, "creation is one revision bump");
        var payload = (SubmissionCreatedEvent.Payload)created.GetPayload();
        payload.CollectionStatus.Should().Be(expectedStatus);
        payload.Revision.Should().Be(2);
    }

    [Fact]
    public void Create_Complete_RaisesCreatedThenCompletedOnOneRevision()
    {
        // Act
        var submission = Create(isComplete: true);

        // Assert
        submission.DomainEvents.Select(e => e.GetType()).Should()
            .Equal(typeof(SubmissionCreatedEvent), typeof(SubmissionCompletedEvent));
        submission.Revision.Should().Be(2);
        var completed = submission.DomainEvents.OfType<SubmissionCompletedEvent>().Single();
        ((SubmissionCompletedEvent.Payload)completed.GetPayload()).Revision.Should().Be(2);
    }

    [Fact]
    public void Create_TestSubmission_CreatedPayloadCarriesTheTestFlag()
    {
        // Act
        var submission = Create(isTest: true);

        // Assert
        var created = submission.DomainEvents.OfType<SubmissionCreatedEvent>().Single();
        ((SubmissionCreatedEvent.Payload)created.GetPayload()).IsTestSubmission.Should().BeTrue();
    }

    [Fact]
    public void FirstSave_OfAPrefilledSubmission_RaisesOneTransitionToInProgress()
    {
        // Arrange
        var submission = CreateCleared(startSubmission: false);
        var revision = submission.Revision;

        // Act
        SaveAnswers(submission, """{"a":1}""");

        // Assert
        AssertSingleTransition(submission, CollectionStatusCodes.NotStarted, CollectionStatusCodes.InProgress);
        submission.Revision.Should().Be(revision + 1);
    }

    [Fact]
    public void InProgressSaves_RaiseNothingAndKeepTheRevision()
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
    public void Complete_RaisesOneTransitionAndStillRaisesCompletedOnOneRevision()
    {
        // Arrange
        var submission = CreateCleared();
        var revision = submission.Revision;

        // Act
        SaveAnswers(submission, """{"a":1}""", isComplete: true);

        // Assert
        AssertSingleTransition(submission, CollectionStatusCodes.InProgress, CollectionStatusCodes.Complete);
        var completed = submission.DomainEvents.OfType<SubmissionCompletedEvent>().Should().ContainSingle().Subject;
        submission.Revision.Should().Be(revision + 1, "complete is one transition");
        ((SubmissionCompletedEvent.Payload)completed.GetPayload()).Revision.Should().Be(revision + 1);
    }

    [Theory]
    [InlineData(true, CollectionStatusCodes.InProgress)]
    [InlineData(false, CollectionStatusCodes.NotStarted)]
    public void ScreenOut_RaisesOneTransitionFromTheCurrentStatus(bool startSubmission, string expectedPrevious)
    {
        // Arrange
        var submission = CreateCleared(startSubmission);
        var revision = submission.Revision;

        // Act
        submission.ScreenOut();

        // Assert
        AssertSingleTransition(submission, expectedPrevious, CollectionStatusCodes.ScreenOut);
        submission.Revision.Should().Be(revision + 1);
        submission.HasStarted.Should().BeTrue();
    }

    [Fact]
    public void ScreenOut_WithAnswers_RaisesOneTransitionAndNoUpdate()
    {
        // Arrange
        var submission = CreateCleared();
        var revision = submission.Revision;

        // Act
        submission.ScreenOut("""{"age":"under_18"}""", FormDefinitionId, formDefinitionFormId: FormId);

        // Assert
        AssertSingleTransition(submission, CollectionStatusCodes.InProgress, CollectionStatusCodes.ScreenOut);
        submission.Revision.Should().Be(revision + 1);
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
    public void Cancel_RaisesOneTransition()
    {
        // Arrange
        var submission = CreateCleared();
        var revision = submission.Revision;

        // Act
        submission.Cancel();

        // Assert
        AssertSingleTransition(submission, CollectionStatusCodes.InProgress, CollectionStatusCodes.Cancelled);
        submission.Revision.Should().Be(revision + 1);
    }

    [Fact]
    public void EnsureStarted_OnAPrefilledSubmission_RaisesOneTransition()
    {
        // Arrange
        var submission = CreateCleared(startSubmission: false);

        // Act
        submission.EnsureStarted();
        submission.EnsureStarted();

        // Assert
        AssertSingleTransition(submission, CollectionStatusCodes.NotStarted, CollectionStatusCodes.InProgress);
    }

    private static void AssertSingleTransition(Submission submission, string previous, string next)
    {
        var changed = submission.DomainEvents.OfType<SubmissionCollectionStatusChangedEvent>().Should().ContainSingle().Subject;
        changed.PreviousCollectionStatus.Should().Be(previous);
        var payload = (SubmissionCollectionStatusChangedEvent.Payload)changed.GetPayload();
        payload.CollectionStatus.Should().Be(next);
        payload.PreviousCollectionStatus.Should().Be(previous);
        payload.Revision.Should().Be(submission.Revision);
        submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().BeEmpty();
    }
}
