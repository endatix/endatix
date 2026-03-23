using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Core.Tests.Abstractions.Authorization;

public sealed class AuthorizationDataExtensionsTests
{
    private static readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _immediateTtl = TimeSpan.FromSeconds(1);

    #region ComputeAuthTtl Tests

    [Fact]
    public void ComputeAuthTtl_NullAuthData_ReturnsDefaultTtl()
    {
        // Arrange
        AuthorizationData? authData = null;
        var utcNow = DateTime.UtcNow;

        // Act
        var result = authData.ComputeAuthTtl(utcNow);

        // Assert
        result.Should().Be(_defaultTtl);
    }

    [Fact]
    public void ComputeAuthTtl_AuthDataNotNull_ReturnsCorrectTtl()
    {
        // Arrange
        var utcNow = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var expiresAt = utcNow.AddMinutes(5);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: [],
            expiresAt: expiresAt);

        // Act
        var result = authData.ComputeAuthTtl(utcNow);

        // Assert
        result.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ComputeAuthTtl_ExpirationInPast_ReturnsImmediateTtl()
    {
        // Arrange
        var utcNow = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var expiresAt = utcNow.AddMinutes(-5);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: [],
            expiresAt: expiresAt);

        // Act
        var result = authData.ComputeAuthTtl(utcNow);

        // Assert
        result.Should().Be(_immediateTtl);
    }

    [Fact]
    public void ComputeAuthTtl_ExpirationExactlyNow_ReturnsImmediateTtl()
    {
        // Arrange
        var utcNow = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: [],
            expiresAt: utcNow);

        // Act
        var result = authData.ComputeAuthTtl(utcNow);

        // Assert
        result.Should().Be(_immediateTtl);
    }

    [Fact]
    public void ComputeAuthTtl_ExpirationInFuture_ReturnsExpectedTtl()
    {
        // Arrange
        var utcNow = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var expiresAt = utcNow.AddHours(1);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: [],
            expiresAt: expiresAt);

        // Act
        var result = authData.ComputeAuthTtl(utcNow);

        // Assert
        result.Should().Be(TimeSpan.FromHours(1));
    }

    #endregion

    #region HasPermission Tests

    [Fact]
    public void HasPermission_NullAuthData_ReturnsFalse()
    {
        // Arrange
        AuthorizationData? authData = null;

        // Act
        var result = authData.HasPermission("forms.read");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_AdminUser_ReturnsTrue()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "admin123",
            tenantId: 1,
            roles: [SystemRole.Admin.Name],
            permissions: []);

        // Act
        var result = authData.HasPermission("any.permission");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_HasPermission_ReturnsTrue()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: ["forms.read", "forms.write"]);

        // Act
        var result = authData.HasPermission("forms.read");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_DoesNotHavePermission_ReturnsFalse()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: ["forms.read"]);

        // Act
        var result = authData.HasPermission("forms.write");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_EmptyPermissions_ReturnsFalse()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: []);

        // Act
        var result = authData.HasPermission("forms.read");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region HasPermissions Tests

    [Fact]
    public void HasPermissions_NullAuthData_ReturnsAllFalse()
    {
        // Arrange
        AuthorizationData? authData = null;
        var permissions = new[] { "forms.read", "forms.write" };

        // Act
        var result = authData.HasPermissions(permissions);

        // Assert
        result.Should().HaveCount(2);
        result["forms.read"].Should().BeFalse();
        result["forms.write"].Should().BeFalse();
    }

    [Fact]
    public void HasPermissions_AdminUser_ReturnsAllTrue()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "admin123",
            tenantId: 1,
            roles: [SystemRole.Admin.Name],
            permissions: []);
        var permissions = new[] { "forms.read", "forms.write", "forms.delete" };

        // Act
        var result = authData.HasPermissions(permissions);

        // Assert
        result.Should().HaveCount(3);
        result["forms.read"].Should().BeTrue();
        result["forms.write"].Should().BeTrue();
        result["forms.delete"].Should().BeTrue();
    }

    [Fact]
    public void HasPermissions_HasSomePermissions_ReturnsCorrectResults()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: ["forms.read", "forms.write"]);
        var permissions = new[] { "forms.read", "forms.write", "forms.delete" };

        // Act
        var result = authData.HasPermissions(permissions);

        // Assert
        result.Should().HaveCount(3);
        result["forms.read"].Should().BeTrue();
        result["forms.write"].Should().BeTrue();
        result["forms.delete"].Should().BeFalse();
    }

    [Fact]
    public void HasPermissions_HasAllPermissions_ReturnsAllTrue()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: ["forms.read", "forms.write"]);
        var permissions = new[] { "forms.read", "forms.write" };

        // Act
        var result = authData.HasPermissions(permissions);

        // Assert
        result.Should().HaveCount(2);
        result["forms.read"].Should().BeTrue();
        result["forms.write"].Should().BeTrue();
    }

    [Fact]
    public void HasPermissions_EmptyPermissionsList_ReturnsEmptyDictionary()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: ["forms.read"]);
        var permissions = Array.Empty<string>();

        // Act
        var result = authData.HasPermissions(permissions);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void HasPermissions_DoesNotHaveAnyPermissions_ReturnsAllFalse()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "user123",
            tenantId: 1,
            roles: [],
            permissions: []);
        var permissions = new[] { "forms.read", "forms.write" };

        // Act
        var result = authData.HasPermissions(permissions);

        // Assert
        result.Should().HaveCount(2);
        result["forms.read"].Should().BeFalse();
        result["forms.write"].Should().BeFalse();
    }

    #endregion
}
