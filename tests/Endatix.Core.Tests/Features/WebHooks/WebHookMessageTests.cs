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
        var formSubmittedOperation = WebHookOperation.FormSubmitted;
        var expectedPayload = new Submission(expectedId.ToString());

        // Act
        var webHookMessage = new WebHookMessage<Submission>(expectedId, formSubmittedOperation, expectedPayload);

        // Assert
        webHookMessage.Id.Should().Be(expectedId);
        webHookMessage.Operation.Should().Be(formSubmittedOperation);
        webHookMessage.Payload.Should().Be(expectedPayload);
        webHookMessage.Action.Should().Be("created");
    }

    [Fact]
    public void WebHookMessage_WithNullPayload_DoesNotThrow()
    {
        // Arrange
        long expectedId = 1;
        var expectedOperation = WebHookOperation.FormSubmitted;
        Submission? nullPayload = null;

        // Act
        var message = new WebHookMessage<Submission>(expectedId, expectedOperation, nullPayload);

        // Assert
        message.Id.Should().Be(expectedId);
        message.Operation.Should().Be(expectedOperation);
        message.Payload.Should().BeNull();
        message.Action.Should().Be("created");
    }
}