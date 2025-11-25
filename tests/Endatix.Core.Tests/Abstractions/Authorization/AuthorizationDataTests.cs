using Endatix.Core.Abstractions.Authorization;
using NSubstitute.Core;

namespace Endatix.Core.Tests.Abstractions.Authorization;

public sealed class AuthorizationDataTests
{
    [Fact]
    public void ForAnonymousUser_ShouldPopulateDefaults()
    {
        // Arrange
        const long tenantId = 99;

        // Act
        var data = AuthorizationData.ForAnonymousUser(tenantId);

        // Assert
        data.UserId.Should().Be("anonymous");
        data.TenantId.Should().Be(tenantId);
        data.Roles.Should().Contain(SystemRole.Public.Name);
        data.Permissions.Should().AllBeEquivalentTo(SystemRole.Public.Permissions);
        data.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public void ForAuthenticatedUser_ShouldAddAuthenticatedRoleAndPermissions()
    {
        // Arrange
        var customRoles = new[] { SystemRole.Admin.Name };
        string[] customPermissions = ["forms.read"];
        string[] expectedPermissions = [Actions.Access.Authenticated, .. customPermissions];

        // Act
        var data = AuthorizationData.ForAuthenticatedUser(
            userId: "123",
            tenantId: 7,
            roles: customRoles,
            permissions: customPermissions);

        // Assert
        data.UserId.Should().Be("123");
        data.Roles.Should().Contain(SystemRole.Authenticated.Name);
        data.Roles.Should().Contain(SystemRole.Admin.Name);
        data.Permissions.Should().BeEquivalentTo(expectedPermissions.ToHashSet());
        data.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public void WithCacheMetadata_ShouldReturnUpdatedCopy()
    {
        // Arrange
        var original = AuthorizationData.ForAuthenticatedUser(
            userId: "123",
            tenantId: 7,
            roles: [],
            permissions: []);
        var cachedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expiresAt = cachedAt.AddMinutes(5);
        const string eTag = "etag-123";

        // Act
        var updated = original.WithCacheMetadata(cachedAt, expiresAt, eTag);

        // Assert
        updated.Should().NotBeSameAs(original);
        updated.CachedAt.Should().Be(cachedAt);
        updated.ExpiresAt.Should().Be(expiresAt);
        updated.ETag.Should().Be(eTag);
        original.CachedAt.Should().NotBe(cachedAt);
    }
}

