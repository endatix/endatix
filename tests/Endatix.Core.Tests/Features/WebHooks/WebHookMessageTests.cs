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
        webHookMessage.Id.Should().Be(expectedId);
        webHookMessage.Operation.Should().Be(submissionCompletedOperation);
        webHookMessage.Payload.Should().Be(expectedPayload);
        webHookMessage.Action.Should().Be("updated");
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
        message.Id.Should().Be(expectedId);
        message.Operation.Should().Be(expectedOperation);
        message.Payload.Should().BeNull();
        message.Action.Should().Be("updated");
    }
}