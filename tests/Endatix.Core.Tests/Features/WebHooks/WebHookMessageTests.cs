using Endatix.Core.Entities;
using Endatix.Core.Features.WebHooks;

namespace Endatix.Core.Tests.Features.WebHooks;

public class WebHookMessageTests
{
    [Fact]
    public void WebHookMessage_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        long expectedId = 1;
        var submissionCompletedOperation = WebHookOperation.SubmissionCompleted;
        var expectedPayload = new Submission(SampleData.TENANT_ID, expectedId.ToString(), formId: 123, formDefinitionId: 456);

        // Act
        var webHookMessage = new WebHookMessage<Submission>(expectedId, submissionCompletedOperation, expectedPayload);

        // Assert
        webHookMessage.id.Should().Be(expectedId);
        webHookMessage.operation.Should().Be(submissionCompletedOperation);
        webHookMessage.payload.Should().Be(expectedPayload);
        webHookMessage.action.Should().Be("updated");
    }

    [Fact]
    public void WebHookMessage_WithNullPayload_DoesNotThrow()
    {
        // Arrange
        long expectedId = 1;
        var expectedOperation = WebHookOperation.SubmissionCompleted;
        Submission? nullPayload = null;

        // Act
        var message = new WebHookMessage<Submission>(expectedId, expectedOperation, nullPayload);

        // Assert
        message.id.Should().Be(expectedId);
        message.operation.Should().Be(expectedOperation);
        message.payload.Should().BeNull();
        message.action.Should().Be("updated");
    }
}