using Endatix.Core.Entities.Identity;

namespace Endatix.Core.Tests.Entities.Identity;

public class PermissionActivationTests
{
    [Fact]
    public void Deactivate_CustomActivePermission_BecomesInactive()
    {
        // Arrange
        var permission = new Permission("custom.read");

        // Act
        permission.Deactivate();

        // Assert
        permission.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_CustomInactivePermission_BecomesActive()
    {
        // Arrange
        var permission = new Permission("custom.read");
        permission.Deactivate();

        // Act
        permission.Activate();

        // Assert
        permission.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Activate_AlreadyActivePermission_StaysActive()
    {
        // Arrange
        var permission = Permission.CreateSystemPermission("sys.read", "desc", "cat");

        // Act
        var act = permission.Activate;

        // Assert
        act.Should().NotThrow();
        permission.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_SystemDefinedPermission_Throws()
    {
        // Arrange
        var permission = Permission.CreateSystemPermission("sys.read", "desc", "cat");

        // Act
        var act = permission.Deactivate;

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
