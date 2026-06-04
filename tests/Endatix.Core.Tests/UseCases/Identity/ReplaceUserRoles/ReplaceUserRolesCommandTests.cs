using Endatix.Core.UseCases.Identity.ReplaceUserRoles;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Identity.ReplaceUserRoles;

public class ReplaceUserRolesCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = 0L;
        var roleNames = new List<string> { "Admin" };

        // Act
        Action act = () => new ReplaceUserRolesCommand(userId, roleNames);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(userId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_NullRoleNames_ThrowsArgumentException()
    {
        // Arrange
        var userId = 1L;

        // Act
        Action act = () => new ReplaceUserRolesCommand(userId, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var userId = 1L;
        var roleNames = new List<string> { "Admin", "Creator" };

        // Act
        var command = new ReplaceUserRolesCommand(userId, roleNames);

        // Assert
        command.UserId.Should().Be(userId);
        command.RoleNames.Should().BeEquivalentTo(roleNames);
    }
}
