using Endatix.Core.Entities;
using Endatix.Core.Events;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.Entities;

public class SubmissionUpdateTests
{
    [Fact]
    public void Update_NullJsonData_ThrowsArgumentNullException()
    {
        // Arrange
        var submission = NewSubmission();

        // Act
        var action = () => submission.Update(null, formDefinitionId: 123, formDefinitionFormId: 123);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithMessage(GetErrorMessage(nameof(Submission.JsonData), Null));
    }

    [Fact]
    public void Update_EmptyJsonData_ThrowsArgumentException()
    {
        // Arrange
        var submission = NewSubmission();

        // Act
        var action = () => submission.Update(string.Empty, formDefinitionId: 123, formDefinitionFormId: 123);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(Submission.JsonData), Empty));
    }

    [Fact]
    public void Update_NegativeFormDefinitionId_ThrowsArgumentException()
    {
        // Arrange
        var submission = NewSubmission();
        const long invalidFormDefinitionId = -1;

        // Act
        var action = () => submission.Update(SampleData.SUBMISSION_JSON_DATA_1, invalidFormDefinitionId, formDefinitionFormId: 123);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(Submission.FormDefinitionId), ZeroOrNegative));
    }

    [Fact]
    public void Update_FormDefinitionFormIdMismatch_ThrowsArgumentException()
    {
        var submission = NewSubmission();

        var act = () => submission.Update(SampleData.SUBMISSION_JSON_DATA_1, formDefinitionId: 789, formDefinitionFormId: 999);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("formDefinitionFormId");
    }

    [Fact]
    public void Update_ValidInput_UpdatesPropertiesCorrectly()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);
        const string updatedJsonData = SampleData.SUBMISSION_JSON_DATA_2;
        const long updatedFormDefinitionId = 789;

        // Act
        submission.Update(updatedJsonData, updatedFormDefinitionId, formDefinitionFormId: 123, isComplete: false, currentPage: 3, metadata: "Updated");

        // Assert
        submission.Should().NotBeNull();
        submission.JsonData.Should().Be(updatedJsonData);
        submission.FormDefinitionId.Should().Be(updatedFormDefinitionId);
        submission.CurrentPage.Should().Be(3);
        submission.Metadata.Should().Be("Updated");
        submission.IsComplete.Should().BeFalse();
        submission.HasStarted.Should().BeTrue();
        submission.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Update_FirstCall_SetsStartedAtOnce()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);
        submission.HasStarted.Should().BeFalse();

        // Act
        submission.Update(SampleData.SUBMISSION_JSON_DATA_2, formDefinitionId: 456, formDefinitionFormId: 123, isComplete: false);
        DateTime? firstStartedAt = submission.StartedAt;

        submission.Update(SampleData.SUBMISSION_JSON_DATA_1, formDefinitionId: 456, formDefinitionFormId: 123, isComplete: false);

        // Assert
        firstStartedAt.Should().NotBeNull();
        submission.StartedAt.Should().Be(firstStartedAt);
    }

    [Fact]
    public void Update_CompleteWithoutPriorStart_SetsStartedAtEqualToCompletedAt()
    {
        // Arrange — create incomplete so StartedAt is null, then complete via Update
        // (Update always EnsureStarted first, so this path is: EnsureStarted then complete)
        var submission = NewSubmission(isComplete: false);

        // Act
        submission.Update(SampleData.SUBMISSION_JSON_DATA_1, formDefinitionId: 456, formDefinitionFormId: 123, isComplete: true);

        // Assert
        submission.IsComplete.Should().BeTrue();
        submission.StartedAt.Should().NotBeNull();
        submission.CompletedAt.Should().NotBeNull();
        submission.StartedAt.Should().BeOnOrBefore(submission.CompletedAt!.Value);
    }

    [Fact]
    public void Update_CompleteAfterStart_PreservesOriginalStartedAt()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);
        var fixedStart = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        submission.EnsureStarted(fixedStart);

        // Act
        submission.Update(SampleData.SUBMISSION_JSON_DATA_1, formDefinitionId: 456, formDefinitionFormId: 123, isComplete: true);

        // Assert
        submission.StartedAt.Should().Be(fixedStart);
        submission.CompletedAt.Should().NotBeNull();
        submission.CompletedAt.Should().BeOnOrAfter(fixedStart);
    }

    [Fact]
    public void EnsureStarted_WhenAlreadyStarted_DoesNotOverwrite()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);
        var fixedStart = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        submission.EnsureStarted(fixedStart);

        // Act
        submission.EnsureStarted(new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc));

        // Assert
        submission.StartedAt.Should().Be(fixedStart);
        submission.CollectionStatus.Code.Should().Be(CollectionStatusCodes.InProgress);
    }

    [Fact]
    public void ScreenOut_IncompleteSubmission_SetsScreenOutAndLeavesIsCompleteFalse()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);

        // Act
        submission.ScreenOut();

        // Assert
        submission.CollectionStatus.Code.Should().Be(CollectionStatusCodes.ScreenOut);
        submission.IsScreenedOut.Should().BeTrue();
        submission.IsComplete.Should().BeFalse();
        submission.CompletedAt.Should().BeNull();
        submission.DomainEvents.Should().ContainSingle(e => e is SubmissionCollectionStatusChangedEvent);
        submission.DomainEvents.Should().NotContain(e => e is SubmissionCompletedEvent);
    }

    [Fact]
    public void ScreenOut_AlreadyScreenedOut_IsNoOp()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);
        submission.ScreenOut();
        var revision = submission.Revision;

        // Act
        submission.ScreenOut(SampleData.SUBMISSION_JSON_DATA_2, formDefinitionId: 456, formDefinitionFormId: 123);

        // Assert
        submission.Revision.Should().Be(revision);
        submission.JsonData.Should().Be(SampleData.SUBMISSION_JSON_DATA_1);
        submission.DomainEvents.OfType<SubmissionCollectionStatusChangedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void ScreenOut_NotStartedSubmission_RecordsStartedAt()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);

        // Act
        submission.ScreenOut();

        // Assert
        submission.StartedAt.Should().NotBeNull();
        submission.CollectionStatus.Code.Should().Be(CollectionStatusCodes.ScreenOut);
    }

    [Fact]
    public void ScreenOut_WithAnswers_SavesAnswersAndScreensOut()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);

        // Act
        submission.ScreenOut(SampleData.SUBMISSION_JSON_DATA_2, formDefinitionId: 456, formDefinitionFormId: 123, currentPage: 2);

        // Assert
        submission.JsonData.Should().Be(SampleData.SUBMISSION_JSON_DATA_2);
        submission.CurrentPage.Should().Be(2);
        submission.IsScreenedOut.Should().BeTrue();
        submission.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void ScreenOut_WithAnswersOnCompleteSubmission_ThrowsAndLeavesSubmissionUnchanged()
    {
        // Arrange
        var submission = NewSubmission(isComplete: true);
        var revision = submission.Revision;
        var eventCount = submission.DomainEvents.Count();

        // Act
        var act = () => submission.ScreenOut(SampleData.SUBMISSION_JSON_DATA_2, formDefinitionId: 456, formDefinitionFormId: 123);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        submission.JsonData.Should().Be(SampleData.SUBMISSION_JSON_DATA_1);
        submission.Revision.Should().Be(revision);
        submission.DomainEvents.Should().HaveCount(eventCount);
    }

    [Fact]
    public void Update_ScreenedOutSubmission_ThrowsInvalidOperationException()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);
        submission.ScreenOut();

        // Act
        var act = () => submission.Update(SampleData.SUBMISSION_JSON_DATA_2, formDefinitionId: 456, formDefinitionFormId: 123, isComplete: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A screened-out submission cannot be changed.");
    }

    [Fact]
    public void ScreenOut_CancelledSubmission_ThrowsInvalidOperationException()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);
        submission.Cancel();

        // Act
        var act = () => submission.ScreenOut();

        // Assert
        act.Should().Throw<InvalidOperationException>();
        submission.CollectionStatus.Code.Should().Be(CollectionStatusCodes.Cancelled);
    }

    [Fact]
    public void Cancel_ScreenedOutSubmission_ThrowsAndKeepsScreenOut()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);
        submission.ScreenOut();

        // Act
        var act = () => submission.Cancel();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A screened-out submission cannot be cancelled.");
        submission.CollectionStatus.Code.Should().Be(CollectionStatusCodes.ScreenOut);
    }

    [Fact]
    public void ScreenOut_EventPayload_CarriesAnswersAndCollectionStatuses()
    {
        // Arrange
        var submission = NewSubmission(isComplete: false);
        submission.ScreenOut();
        var domainEvent = submission.DomainEvents.OfType<SubmissionCollectionStatusChangedEvent>().Single();

        // Act
        var payload = (SubmissionCollectionStatusChangedEvent.Payload)domainEvent.GetPayload();

        // Assert
        payload.CollectionStatus.Should().Be(CollectionStatusCodes.ScreenOut);
        payload.PreviousCollectionStatus.Should().Be(CollectionStatusCodes.NotStarted);
        payload.JsonData.Should().Be(SampleData.SUBMISSION_JSON_DATA_1);
        payload.IsComplete.Should().BeFalse();
        payload.StartedAt.Should().NotBeNull();
    }

    private static Submission NewSubmission(bool isComplete = true) =>
        Submission.Create(new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            FormId: 123,
            FormDefinitionId: 456,
            JsonData: SampleData.SUBMISSION_JSON_DATA_1,
            IsComplete: isComplete));
}
