using Endatix.Core.Features.WebHooks;

namespace Endatix.Core.Tests.Features.WebHooks;

public class WebHookOperationTests
{
    [Fact]
    public void FormSubmitted_Operation_ShouldHaveCorrectValues()
    {
        // Arrange & Act
        var formSubmitted = WebHookOperation.FormSubmitted;

        // Assert
        formSubmitted.Should().NotBeNull();
        formSubmitted.EventName.Should().Be("form_submitted");
        formSubmitted.Entity.Should().Be("Submission");
        formSubmitted.Action.Should().Be(ActionNames.Created);
        formSubmitted.Action.GetDisplayName().Should().Be("created");
    }

    [Fact]
    public void WebHookOperation_Equality_ShouldBeValueBased()
    {
        // Arrange
        var formSubmittedOperation1 = WebHookOperation.FormSubmitted;
        var formSubmittedOperation2 = WebHookOperation.FormSubmitted;

        // Act & Assert
        formSubmittedOperation1.Should().Be(formSubmittedOperation2);
    }
}