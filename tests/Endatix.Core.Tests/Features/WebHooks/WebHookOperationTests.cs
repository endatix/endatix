using Endatix.Core.Features.WebHooks;

namespace Endatix.Core.Tests.Features.WebHooks;

public class WebHookOperationTests
{
    [Fact]
    public void SubmissionCompleted_Operation_ShouldHaveCorrectValues()
    {
        // Arrange & Act
        var submissionCompleted = WebHookOperation.SubmissionCompleted;

        // Assert
        submissionCompleted.Should().NotBeNull();
        submissionCompleted.EventName.Should().Be("submission_completed");
        submissionCompleted.Entity.Should().Be("Submission");
        submissionCompleted.Action.Should().Be(ActionName.Updated);
        submissionCompleted.Action.GetDisplayName().Should().Be("updated");
    }

    [Fact]
    public void WebHookOperation_Equality_ShouldBeValueBased()
    {
        // Arrange
        var submissionCompletedOperation1 = WebHookOperation.SubmissionCompleted;
        var submissionCompletedOperation2 = WebHookOperation.SubmissionCompleted;

        // Act & Assert
        submissionCompletedOperation1.Should().Be(submissionCompletedOperation2);
    }
}