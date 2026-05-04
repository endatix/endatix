using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;

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
        data.UserId.Should().Be(AuthorizationData.ANONYMOUS_USER_ID);
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

    [Fact]
    public void JsonDeserialize_DerivesIsAdminFromRoles_IgnoringStaleIsAdminProperty()
    {
        const string json = """
            {
              "UserId": "1",
              "TenantId": 2,
              "Roles": ["Authenticated", "Admin"],
              "Permissions": ["perm.a"],
              "IsAdmin": false,
              "CachedAt": "2025-01-01T00:00:00Z",
              "ExpiresAt": "2025-01-01T00:10:00Z",
              "ETag": ""
            }
            """;
        var data = JsonSerializer.Deserialize<AuthorizationData>(json);
        data.Should().NotBeNull();
        data!.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public void JsonRoundTrip_PreservesIsAdminFromRoles()
    {
        var original = AuthorizationData.ForAuthenticatedUser(
            userId: "u",
            tenantId: 9,
            roles: [SystemRole.PlatformAdmin.Name],
            permissions: ["p"]);
        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<AuthorizationData>(json);
        roundTripped.Should().NotBeNull();
        roundTripped!.IsAdmin.Should().BeTrue();
        roundTripped.Roles.Should().Contain(SystemRole.PlatformAdmin.Name);
    }
}

