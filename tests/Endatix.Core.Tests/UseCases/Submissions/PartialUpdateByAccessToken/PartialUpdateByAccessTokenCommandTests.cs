using Endatix.Core.UseCases.Submissions.PartialUpdateByAccessToken;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Submissions.PartialUpdateByAccessToken;

public class PartialUpdateByAccessTokenCommandTests
{
    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var accessToken = "12345.1234567890.rw.signature";
        var formId = 123L;
        var isComplete = true;
        var currentPage = 5;
        var jsonData = "{\"field\":\"value\"}";
        var metadata = "{\"meta\":\"data\"}";

        // Act
        var command = new PartialUpdateByAccessTokenCommand(
            accessToken,
            formId,
            isComplete,
            currentPage,
            jsonData,
            metadata);

        // Assert
        command.AccessToken.Should().Be(accessToken);
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
        var accessToken = "12345.1234567890.rw.signature";
        var formId = 123L;

        // Act
        var command = new PartialUpdateByAccessTokenCommand(
            accessToken,
            formId,
            null,
            null,
            null,
            null);

        // Assert
        command.AccessToken.Should().Be(accessToken);
        command.FormId.Should().Be(formId);
        command.IsComplete.Should().BeNull();
        command.CurrentPage.Should().BeNull();
        command.JsonData.Should().BeNull();
        command.Metadata.Should().BeNull();
    }

    [Fact]
    public void Constructor_NullAccessToken_ThrowsArgumentException()
    {
        // Act
        var act = () => new PartialUpdateByAccessTokenCommand(
            null!,
            123L,
            null,
            null,
            null,
            null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyAccessToken_ThrowsArgumentException()
    {
        // Act
        var act = () => new PartialUpdateByAccessTokenCommand(
            "",
            123L,
            null,
            null,
            null,
            null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ZeroFormId_ThrowsArgumentException()
    {
        // Act
        var act = () => new PartialUpdateByAccessTokenCommand(
            "token",
            0,
            null,
            null,
            null,
            null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeFormId_ThrowsArgumentException()
    {
        // Act
        var act = () => new PartialUpdateByAccessTokenCommand(
            "token",
            -1,
            null,
            null,
            null,
            null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeCurrentPage_ThrowsArgumentException()
    {
        // Act
        var act = () => new PartialUpdateByAccessTokenCommand(
            "token",
            123L,
            null,
            -1,
            null,
            null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ZeroCurrentPage_SetsPropertyCorrectly()
    {
        // Arrange
        var accessToken = "token";
        var formId = 123L;
        var currentPage = 0;

        // Act
        var command = new PartialUpdateByAccessTokenCommand(
            accessToken,
            formId,
            null,
            currentPage,
            null,
            null);

        // Assert
        command.CurrentPage.Should().Be(currentPage);
    }
}
