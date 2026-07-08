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
    public void Completing_on_creation_raises_the_event_and_bumps_revision()
    {
        var submission = Create(isComplete: true);

        submission.DomainEvents.OfType<SubmissionCompletedEvent>().Should().ContainSingle();
        submission.IsComplete.Should().BeTrue();
        submission.Revision.Should().Be(2);
    }

    [Fact]
    public void Incomplete_creation_does_not_raise()
    {
        var submission = Create(isComplete: false);

        submission.DomainEvents.OfType<SubmissionCompletedEvent>().Should().BeEmpty();
        submission.IsComplete.Should().BeFalse();
        submission.Revision.Should().Be(1);
    }

    [Fact]
    public void Update_to_complete_raises_once()
    {
        var submission = Create(isComplete: false);

        submission.Update("{\"a\":1}", FormDefinitionId, formDefinitionFormId: FormId, isComplete: true);

        submission.DomainEvents.OfType<SubmissionCompletedEvent>().Should().ContainSingle();
        submission.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Re_saving_an_already_complete_submission_does_not_raise_again()
    {
        var submission = Create(isComplete: true);
        submission.ClearDomainEvents(); // drop the creation-time completion event

        submission.Update("{\"a\":1}", FormDefinitionId, formDefinitionFormId: FormId, isComplete: true);

        submission.DomainEvents.OfType<SubmissionCompletedEvent>().Should().BeEmpty("it was already complete");
    }

    [Fact]
    public void Update_with_only_metadata_change_on_complete_submission_raises_updated_with_metadata_kind()
    {
        var submission = Create(isComplete: true);
        submission.ClearDomainEvents();

        submission.Update("{}", FormDefinitionId, formDefinitionFormId: FormId, isComplete: true, metadata: "{\"tag\":\"vip\"}");

        var updated = submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().ContainSingle().Subject;
        updated.ChangeKind.Should().Be(SubmissionChangeKinds.Metadata);
    }

    [Fact]
    public void Update_with_answer_change_on_complete_submission_raises_updated_with_answers_kind()
    {
        var submission = Create(isComplete: true);
        submission.ClearDomainEvents();

        submission.Update("{\"a\":1}", FormDefinitionId, formDefinitionFormId: FormId, isComplete: true);

        var updated = submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().ContainSingle().Subject;
        updated.ChangeKind.Should().Be(SubmissionChangeKinds.Answers);
    }

    [Fact]
    public void Update_with_only_current_page_change_on_complete_submission_does_not_raise_updated()
    {
        var submission = Create(isComplete: true);
        submission.ClearDomainEvents();

        submission.Update("{}", FormDefinitionId, formDefinitionFormId: FormId, isComplete: true, currentPage: 99);

        submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void SetSubmitter_on_complete_submission_raises_updated_with_submitter_kind()
    {
        var submission = Create(isComplete: true);
        submission.ClearDomainEvents();

        submission.SetSubmitter(42, "display-42", "{\"name\":\"Ada\"}");

        var updated = submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().ContainSingle().Subject;
        updated.ChangeKind.Should().Be(SubmissionChangeKinds.Submitter);
    }

    [Fact]
    public void UpdateStatus_with_new_status_raises_status_changed_and_bumps_revision()
    {
        var submission = Create(isComplete: true);
        submission.ClearDomainEvents();
        var revisionBefore = submission.Revision;

        submission.UpdateStatus(SubmissionStatus.Approved);

        submission.Status.Should().Be(SubmissionStatus.Approved);
        submission.Revision.Should().Be(revisionBefore + 1);
        var statusChanged = submission.DomainEvents.OfType<SubmissionStatusChangedEvent>().Should().ContainSingle().Subject;
        statusChanged.PreviousStatus.Should().Be(SubmissionStatus.New);
    }

    [Fact]
    public void UpdateStatus_with_same_status_does_not_raise_or_bump_revision()
    {
        var submission = Create(isComplete: true);
        submission.ClearDomainEvents();
        var revisionBefore = submission.Revision;

        submission.UpdateStatus(SubmissionStatus.New);

        submission.Revision.Should().Be(revisionBefore);
        submission.DomainEvents.Should().BeEmpty();
    }
}
