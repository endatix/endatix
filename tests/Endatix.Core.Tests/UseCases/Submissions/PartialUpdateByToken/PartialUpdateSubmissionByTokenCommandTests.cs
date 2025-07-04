using Endatix.Core.UseCases.Submissions.PartialUpdateByToken;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Submissions.PartialUpdateByToken;

public class PartialUpdateSubmissionByTokenCommandTests
{
    [Fact]
    public void Constructor_NullOrEmptyToken_ThrowsArgumentException()
    {
        // Arrange
        var token = "";
        var formId = 1L;

        // Act
        Action act = () => new PartialUpdateSubmissionByTokenCommand(
            token: token,
            formId: formId,
            isComplete: null,
            currentPage: null,
            jsonData: null,
            metadata: null,
            reCaptchaToken: null
        );

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(token), Empty));
    }

    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var token = "valid-token";
        var formId = -1L;

        // Act
        Action act = () => new PartialUpdateSubmissionByTokenCommand(
            token,
            formId,
            null,
            null,
            null,
            null,
            null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_NegativeCurrentPage_ThrowsArgumentException()
    {
        // Arrange
        var token = "valid-token";
        var formId = 1L;
        int? negativeCurrentPage = -1;

        // Act
        Action act = () => new PartialUpdateSubmissionByTokenCommand(
            token: token,
            formId: formId,
            isComplete: false,
            currentPage: negativeCurrentPage,
            jsonData: null,
            metadata: null,
            reCaptchaToken: null
        );

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage("currentPage.Value", Negative));
    }

    [Fact]
    public void Constructor_OmittedCurrentPage_SetsPropertiesCorrectly()
    {
        // Arrange
        var token = "valid-token";
        var formId = 1L;
        var isComplete = true;
        int? currentPage = null;
        var jsonData = "{}";
        var metadata = string.Empty;
        var reCaptchaToken = "valid-reCaptchaToken";

        // Act
        var command = new PartialUpdateSubmissionByTokenCommand(
            token: token,
            formId: formId,
            isComplete: isComplete,
            currentPage: currentPage,
            jsonData: jsonData,
            metadata: metadata,
            reCaptchaToken: reCaptchaToken
        );

        // Assert
        command.Should().NotBeNull();
        command.CurrentPage.Should().Be(currentPage);
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var token = "valid-token";
        var formId = 1L;
        var isComplete = true;
        var currentPage = 2;
        var jsonData = "{\"key\":\"value\"}";
        var metadata = "{\"meta\":\"data\"}";
        var reCaptchaToken = "valid-reCaptchaToken";

        // Act
        var command = new PartialUpdateSubmissionByTokenCommand(
            token: token,
            formId: formId,
            isComplete: isComplete,
            currentPage: currentPage,
            jsonData: jsonData,
            metadata: metadata,
            reCaptchaToken: reCaptchaToken);

        // Assert
        command.Token.Should().Be(token);
        command.FormId.Should().Be(formId);
        command.IsComplete.Should().Be(isComplete);
        command.CurrentPage.Should().Be(currentPage);
        command.JsonData.Should().Be(jsonData);
        command.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void Constructor_NullOptionalParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var token = "valid-token";
        var formId = 1L;

        // Act
        var command = new PartialUpdateSubmissionByTokenCommand(
            token: token,
            formId: formId,
            isComplete: null,
            currentPage: null,
            jsonData: null,
            metadata: null,
            reCaptchaToken: null
        );

        // Assert
        command.Token.Should().Be(token);
        command.FormId.Should().Be(formId);
        command.IsComplete.Should().BeNull();
        command.CurrentPage.Should().BeNull();
        command.JsonData.Should().BeNull();
        command.Metadata.Should().BeNull();
        command.ReCaptchaToken.Should().BeNull();
    }
}
