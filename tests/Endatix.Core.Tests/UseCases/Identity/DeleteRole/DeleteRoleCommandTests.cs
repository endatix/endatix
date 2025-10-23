using Endatix.Core.UseCases.Identity.DeleteRole;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Identity.DeleteRole;

public class DeleteRoleCommandTests
{
    [Fact]
    public void Constructor_NullOrWhiteSpaceRoleName_ThrowsArgumentException()
    {
        // Arrange
        var roleName = "";

        // Act
        Action act = () => new DeleteRoleCommand(roleName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(roleName), Empty));
    }

    [Fact]
    public void Constructor_NullRoleName_ThrowsArgumentException()
    {
        // Arrange
        string roleName = null!;

        // Act
        Action act = () => new DeleteRoleCommand(roleName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(roleName), Null));
    }

    [Fact]
    public void Constructor_ValidRoleName_SetsPropertyCorrectly()
    {
        // Arrange
        var roleName = "Manager";

        // Act
        var command = new DeleteRoleCommand(roleName);

        // Assert
        command.RoleName.Should().Be(roleName);
    }
}
