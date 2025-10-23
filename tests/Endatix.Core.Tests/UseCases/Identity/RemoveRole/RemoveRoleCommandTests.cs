using Endatix.Core.UseCases.Identity.RemoveRole;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Identity.RemoveRole;

public class RemoveRoleCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = 0L;
        var roleName = "Admin";

        // Act
        Action act = () => new RemoveRoleCommand(userId, roleName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(userId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_NullOrWhiteSpaceRoleName_ThrowsArgumentException()
    {
        // Arrange
        var userId = 1L;
        var roleName = "";

        // Act
        Action act = () => new RemoveRoleCommand(userId, roleName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(roleName), Empty));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var userId = 1L;
        var roleName = "Admin";

        // Act
        var command = new RemoveRoleCommand(userId, roleName);

        // Assert
        command.UserId.Should().Be(userId);
        command.RoleName.Should().Be(roleName);
    }
}
