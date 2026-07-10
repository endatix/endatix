using Endatix.Core.Entities;
using Endatix.Core.Events;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// Phase 3b: the Submission aggregate raises <see cref="SubmissionCompletedEvent"/> once on the
/// incomplete→complete transition (from the ctor or Update), captured to the outbox → webhook.
/// </summary>
public class SubmissionIntegrationEventTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;

    private static Submission Create(bool isComplete) =>
        Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: FormDefinitionId,
            JsonData: "{}",
            IsComplete: isComplete));

    [Fact]
    public void Submission_Create_WhenCompleted_RaisesCompletedEventAndBumpsRevision()
    {
        // Act
        Submission submission = Create(isComplete: true);

        // Assert
        submission.DomainEvents.OfType<SubmissionCompletedEvent>().Should().ContainSingle();
        submission.IsComplete.Should().BeTrue();
        submission.Revision.Should().Be(2);
    }

    [Fact]
    public void Submission_Create_WhenIncomplete_DoesNotRaiseCompletedEvent()
    {
        // Act
        Submission submission = Create(isComplete: false);

        // Assert
        submission.DomainEvents.OfType<SubmissionCompletedEvent>().Should().BeEmpty();
        submission.IsComplete.Should().BeFalse();
        submission.Revision.Should().Be(1);
    }

    [Fact]
    public void Submission_Update_WhenTransitioningToComplete_RaisesCompletedEventOnce()
    {
        // Arrange
        Submission submission = Create(isComplete: false);

        // Act
        submission.Update("{\"a\":1}", FormDefinitionId, formDefinitionFormId: FormId, isComplete: true);

        // Assert
        submission.DomainEvents.OfType<SubmissionCompletedEvent>().Should().ContainSingle();
        submission.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Submission_Update_WhenAlreadyComplete_DoesNotRaiseCompletedEventAgain()
    {
        // Arrange
        Submission submission = Create(isComplete: true);
        submission.ClearDomainEvents();

        // Act
        submission.Update("{\"a\":1}", FormDefinitionId, formDefinitionFormId: FormId, isComplete: true);

        // Assert
        submission.DomainEvents.OfType<SubmissionCompletedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Submission_Update_WithMetadataChangeOnCompleteSubmission_RaisesUpdatedWithMetadataKind()
    {
        // Arrange
        Submission submission = Create(isComplete: true);
        submission.ClearDomainEvents();

        // Act
        submission.Update("{}", FormDefinitionId, formDefinitionFormId: FormId, isComplete: true, metadata: "{\"tag\":\"vip\"}");

        // Assert
        SubmissionUpdatedEvent updated = submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().ContainSingle().Subject;
        updated.ChangeKind.Should().Be(SubmissionChangeKinds.Metadata);
    }

    [Fact]
    public void Submission_Update_WithAnswerChangeOnCompleteSubmission_RaisesUpdatedWithAnswersKind()
    {
        // Arrange
        Submission submission = Create(isComplete: true);
        submission.ClearDomainEvents();

        // Act
        submission.Update("{\"a\":1}", FormDefinitionId, formDefinitionFormId: FormId, isComplete: true);

        // Assert
        SubmissionUpdatedEvent updated = submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().ContainSingle().Subject;
        updated.ChangeKind.Should().Be(SubmissionChangeKinds.Answers);
    }

    [Fact]
    public void Submission_Update_WithOnlyCurrentPageChangeOnCompleteSubmission_DoesNotRaiseUpdatedEvent()
    {
        // Arrange
        Submission submission = Create(isComplete: true);
        submission.ClearDomainEvents();

        // Act
        submission.Update("{}", FormDefinitionId, formDefinitionFormId: FormId, isComplete: true, currentPage: 99);

        // Assert
        submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Submission_SetSubmitter_OnCompleteSubmission_RaisesUpdatedWithSubmitterKind()
    {
        // Arrange
        Submission submission = Create(isComplete: true);
        submission.ClearDomainEvents();

        // Act
        submission.SetSubmitter(42, "display-42", "{\"name\":\"Ada\"}");

        // Assert
        SubmissionUpdatedEvent updated = submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().ContainSingle().Subject;
        updated.ChangeKind.Should().Be(SubmissionChangeKinds.Submitter);
    }

    [Fact]
    public void Submission_SetSubmitter_WithDisplayIdOnlyChangeOnCompleteSubmission_RaisesUpdatedWithSubmitterKind()
    {
        // Arrange
        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: FormDefinitionId,
            JsonData: "{}",
            IsComplete: true,
            SubmitterId: 42,
            SubmitterDisplayId: "display-old",
            SubmitterProfileSnapshot: "{\"name\":\"Ada\"}"));
        submission.ClearDomainEvents();

        // Act
        submission.SetSubmitter(42, "display-new", "{\"name\":\"Ada\"}");

        // Assert
        SubmissionUpdatedEvent updated = submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().ContainSingle().Subject;
        updated.ChangeKind.Should().Be(SubmissionChangeKinds.Submitter);
        submission.SubmittedBy.Should().Be("42");
        submission.SubmitterDisplayId.Should().Be("display-new");
    }

    [Fact]
    public void Submission_SetSubmitter_WithUnchangedFieldsOnCompleteSubmission_DoesNotRaiseUpdatedEvent()
    {
        // Arrange
        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: FormDefinitionId,
            JsonData: "{}",
            IsComplete: true,
            SubmitterId: 42,
            SubmitterDisplayId: "display-42",
            SubmitterProfileSnapshot: "{\"name\":\"Ada\"}"));
        submission.ClearDomainEvents();

        // Act
        submission.SetSubmitter(42, "display-42", "{\"name\":\"Ada\"}");

        // Assert
        submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Submission_UpdateStatus_WithNewStatus_RaisesStatusChangedAndBumpsRevision()
    {
        // Arrange
        Submission submission = Create(isComplete: true);
        submission.ClearDomainEvents();
        long revisionBefore = submission.Revision;

        // Act
        submission.UpdateStatus(SubmissionStatus.Approved);

        // Assert
        submission.Status.Should().Be(SubmissionStatus.Approved);
        submission.Revision.Should().Be(revisionBefore + 1);
        SubmissionStatusChangedEvent statusChanged = submission.DomainEvents.OfType<SubmissionStatusChangedEvent>().Should().ContainSingle().Subject;
        statusChanged.PreviousStatus.Should().Be(SubmissionStatus.New);
    }

    [Fact]
    public void Submission_UpdateStatus_WithSameStatus_DoesNotRaiseOrBumpRevision()
    {
        // Arrange
        Submission submission = Create(isComplete: true);
        submission.ClearDomainEvents();
        long revisionBefore = submission.Revision;

        // Act
        submission.UpdateStatus(SubmissionStatus.New);

        // Assert
        submission.Revision.Should().Be(revisionBefore);
        submission.DomainEvents.Should().BeEmpty();
    }
}
