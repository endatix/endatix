using Endatix.Core.UseCases.Identity.CreateRole;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Identity.CreateRole;

public class CreateRoleCommandTests
{
    [Fact]
    public void Constructor_NullOrWhiteSpaceName_ThrowsArgumentException()
    {
        // Arrange
        var name = "";
        var description = "Test role";
        var permissions = new List<string> { "forms.read" };

        // Act
        Action act = () => new CreateRoleCommand(name, description, permissions);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(name), Empty));
    }

    [Fact]
    public void Constructor_NullPermissions_ThrowsArgumentException()
    {
        // Arrange
        var name = "Manager";
        var description = "Manager role";
        List<string> permissions = null!;

        // Act
        Action act = () => new CreateRoleCommand(name, description, permissions);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(permissions), Null));
    }

    [Fact]
    public void Constructor_EmptyPermissions_ThrowsArgumentException()
    {
        // Arrange
        var name = "Manager";
        var description = "Manager role";
        var permissions = new List<string>();

        // Act
        Action act = () => new CreateRoleCommand(name, description, permissions);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(permissions), Empty));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var name = "Manager";
        var description = "Manager role";
        var permissions = new List<string> { "forms.read", "forms.write" };

        // Act
        var command = new CreateRoleCommand(name, description, permissions);

        // Assert
        command.Name.Should().Be(name);
        command.Description.Should().Be(description);
        command.Permissions.Should().BeEquivalentTo(permissions);
    }

    [Fact]
    public void Constructor_NullDescription_SetsPropertiesCorrectly()
    {
        // Arrange
        var name = "Manager";
        string? description = null;
        var permissions = new List<string> { "forms.read" };

        // Act
        var command = new CreateRoleCommand(name, description, permissions);

        // Assert
        command.Name.Should().Be(name);
        command.Description.Should().BeNull();
        command.Permissions.Should().BeEquivalentTo(permissions);
    }
}
