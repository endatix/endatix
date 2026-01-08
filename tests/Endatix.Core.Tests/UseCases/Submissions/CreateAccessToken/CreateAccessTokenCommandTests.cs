using Endatix.Core.UseCases.Submissions.CreateAccessToken;

namespace Endatix.Core.Tests.UseCases.Submissions.CreateAccessToken;

public class CreateAccessTokenCommandTests
{
    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var expiryMinutes = 60;
        var permissions = new[] { "view", "edit" };

        // Act
        var command = new CreateAccessTokenCommand(formId, submissionId, expiryMinutes, permissions);

        // Assert
        command.FormId.Should().Be(formId);
        command.SubmissionId.Should().Be(submissionId);
        command.ExpiryMinutes.Should().Be(expiryMinutes);
        command.Permissions.Should().BeEquivalentTo(permissions);
    }

    [Fact]
    public void Constructor_AllPermissions_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 123L;
        var submissionId = 456L;
        var expiryMinutes = 1440;
        var permissions = new[] { "view", "edit", "export" };

        // Act
        var command = new CreateAccessTokenCommand(formId, submissionId, expiryMinutes, permissions);

        // Assert
        command.FormId.Should().Be(formId);
        command.SubmissionId.Should().Be(submissionId);
        command.ExpiryMinutes.Should().Be(expiryMinutes);
        command.Permissions.Should().BeEquivalentTo(permissions);
    }

    [Fact]
    public void Constructor_ZeroFormId_ThrowsArgumentException()
    {
        // Act
        var act = () => new CreateAccessTokenCommand(0, 456L, 60, new[] { "view" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeFormId_ThrowsArgumentException()
    {
        // Act
        var act = () => new CreateAccessTokenCommand(-1, 456L, 60, new[] { "view" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ZeroSubmissionId_ThrowsArgumentException()
    {
        // Act
        var act = () => new CreateAccessTokenCommand(123L, 0, 60, new[] { "view" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeSubmissionId_ThrowsArgumentException()
    {
        // Act
        var act = () => new CreateAccessTokenCommand(123L, -1, 60, new[] { "view" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ZeroExpiryMinutes_ThrowsArgumentException()
    {
        // Act
        var act = () => new CreateAccessTokenCommand(123L, 456L, 0, new[] { "view" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeExpiryMinutes_ThrowsArgumentException()
    {
        // Act
        var act = () => new CreateAccessTokenCommand(123L, 456L, -1, new[] { "view" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullPermissions_ThrowsArgumentException()
    {
        // Act
        var act = () => new CreateAccessTokenCommand(123L, 456L, 60, null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyPermissions_ThrowsArgumentException()
    {
        // Act
        var act = () => new CreateAccessTokenCommand(123L, 456L, 60, Array.Empty<string>());

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
