using Endatix.Core.Entities;
using FluentAssertions;
using static Endatix.Core.Tests.Entities.SubmissionLifecycleTestKit;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// Lifecycle events of a submission loaded in a status no built-in save sets: a drifted row, a lapsed, closed or
/// host-registered code, and a code nobody registered.
/// </summary>
public class SubmissionStatusCodeLifecycleTests
{
    [Fact]
    public void Update_InProgressOnAHostActiveCode_KeepsTheCodeAndRaisesNothing()
    {
        // Arrange
        var submission = Loaded(Lifecycle.HostCollectionStatuses.UnderReview);
        var revision = submission.Revision;

        // Act
        SaveAnswers(submission, """{"a":1}""");

        // Assert
        submission.CollectionStatus.Code.Should().Be(Lifecycle.HostCollectionStatuses.UnderReview);
        submission.DomainEvents.Should().BeEmpty();
        submission.Revision.Should().Be(revision);
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
    public void Update_CompletingADriftedRow_ReportsTheEffectiveStatusAsPrevious()
    {
        // Arrange
        var submission = Loaded(CollectionStatusCodes.Complete);

        // Act
        SaveAnswers(submission, """{"a":1}""", isComplete: true);

        // Assert
        var payload = (SubmissionLifecyclePayload)SingleLifecycleEvent(submission).GetPayload();
        payload.PreviousCollectionStatus.Should().Be(new CollectionStatusRef(CollectionStatusCodes.InProgress, "active"));
        payload.CollectionStatus.Should().Be(new CollectionStatusRef(CollectionStatusCodes.Complete, "completed"));
    }

    [Fact]
    public void Update_InProgressOnAbandoned_RaisesResumed()
    {
        // Arrange
        var submission = Loaded(CollectionStatusCodes.Abandoned);

        // Act
        SaveAnswers(submission, """{"a":1}""");

        // Assert
        AssertSingle(submission, (SubmissionLifecycleEvent.EventTypes.Resumed, CollectionStatusCodes.Abandoned, CollectionStatusCodes.InProgress));
    }

    [Fact]
    public void Update_InProgressOnAnUnregisteredCode_KeepsTheCodeAndRaisesNothing()
    {
        // Arrange
        var submission = Loaded("removed_from_config");

        // Act
        SaveAnswers(submission, """{"a":1}""");

        // Assert
        submission.CollectionStatus.Code.Should().Be("removed_from_config");
        submission.JsonData.Should().Be("""{"a":1}""");
        submission.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_CompletingAnUnregisteredCode_RaisesCompletedFromActive()
    {
        // Arrange
        var submission = Loaded("removed_from_config");

        // Act
        SaveAnswers(submission, """{"a":1}""", isComplete: true);

        // Assert
        var payload = (SubmissionLifecyclePayload)SingleLifecycleEvent(submission).GetPayload();
        SingleLifecycleEvent(submission).EventType.Should().Be(SubmissionLifecycleEvent.EventTypes.Completed);
        payload.PreviousCollectionStatus.Should().Be(new CollectionStatusRef("removed_from_config", "active"));
    }

    [Fact]
    public void Update_InProgressOnLapsed_RaisesResumed()
    {
        // Arrange
        var submission = Loaded(CollectionStatusCodes.Expired);

        // Act
        SaveAnswers(submission, """{"a":1}""");

        // Assert
        AssertSingle(submission, (SubmissionLifecycleEvent.EventTypes.Resumed, CollectionStatusCodes.Expired, CollectionStatusCodes.InProgress));
    }

    [Theory]
    [InlineData(CollectionStatusCodes.ScreenOut)]
    [InlineData(CollectionStatusCodes.Cancelled)]
    [InlineData(CollectionStatusCodes.QuotaFull)]
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
}
