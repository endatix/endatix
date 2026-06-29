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
        Submission.Create(TenantId, "{}", FormId, FormDefinitionId, new SubmissionCreateOptions(IsComplete: isComplete));

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
}
