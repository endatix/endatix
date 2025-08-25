using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.MyAccount.ChangePassword;
using MediatR;

namespace Endatix.Core.Tests.UseCases.MyAccount.ChangePassword;

public class ChangePasswordCommandTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        // Arrange
        var userId = 123L;
        const string currentPassword = "currentPass123";
        const string newPassword = "newPass123";

        // Act
        var command = new ChangePasswordCommand(userId, currentPassword, newPassword);

        // Assert
        command.UserId.Should().Be(userId);
        command.CurrentPassword.Should().Be(currentPassword);
        command.NewPassword.Should().Be(newPassword);
    }

    [Fact]
    public void Command_ShouldBeImmutable()
    {
        // Arrange
        var userId = 123L;
        var command = new ChangePasswordCommand(userId, "current", "new");

        // Act & Assert
        command.Should().BeAssignableTo<IRequest<Result<string>>>();
        command.GetType().Should().BeAssignableTo<IEquatable<ChangePasswordCommand>>();
    }

    [Fact]
    public void Command_ShouldThrowException_WhenCurrentPasswordIsEmpty()
    {
        // Arrange & Act
        var userId = 123L;
        var command = () => new ChangePasswordCommand(userId, "", "new");

        // Assert
        command.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Command_ShouldThrowException_WhenCurrentNewPasswordIsEmpty()
    {
        // Arrange & Act
        var userId = 123L;
        var command = () => new ChangePasswordCommand(userId, "current", "");

        // Assert
        command.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Command_ShouldNotThrowException_WhenUserIdIsNull()
    {
        // Arrange & Act
        var userId = null as long?;
        var command = () => new ChangePasswordCommand(userId, "current", "new");

        // Assert
        command.Should().NotThrow<ArgumentException>();
    }
}
