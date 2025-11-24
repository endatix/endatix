using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Infrastructure.Tests.Identity;

public sealed class AuthorizedIdentityTests
{
    [Fact]
    public void Constructor_AddsAuthorizationClaims()
    {
        // Arrange
        var cachedAt = DateTime.UtcNow;
        var expiresAt = cachedAt.AddMinutes(30);

        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "123",
            tenantId: 42,
            roles: [SystemRole.Admin.Name],
            permissions: ["forms.read"],
            cachedAt: cachedAt,
            expiresAt: expiresAt,
            eTag: "etag-123");

        // Act
        var identity = new AuthorizedIdentity(authData);

        // Assert
        identity.IsHydrated.Should().BeTrue();
        identity.TenantId.Should().Be(42);
        identity.Roles.Should().Contain(SystemRole.Admin.Name);
        identity.Permissions.Should().Contain("forms.read");
        identity.IsAdmin.Should().BeTrue();
        identity.CachedAt.ToString("O").Should().Be(cachedAt.ToString("O"));
        identity.CacheExpiresIn.ToString("O").Should().Be(expiresAt.ToString("O"));
        identity.ETag.Should().Be("etag-123");
    }

    [Fact]
    public void TenantId_ReturnsDefault_WhenClaimMissing()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "123",
            tenantId: 7,
            roles: [],
            permissions: [],
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(1),
            eTag: string.Empty);

        // Act
        var identity = new AuthorizedIdentity(authData);
        var tenantClaim = identity.FindFirst(ClaimNames.TenantId);
        tenantClaim.Should().NotBeNull();
        identity.RemoveClaim(tenantClaim!);

        // Assert
        identity.TenantId.Should().Be(AuthConstants.DEFAULT_TENANT_ID);
    }

    [Fact]
    public void Permissions_ReturnsDistinctValues()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "123",
            tenantId: 1,
            roles: [],
            permissions: ["forms.read"],
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(5),
            eTag: string.Empty);

        // Act
        var identity = new AuthorizedIdentity(authData);
        identity.AddClaim(new Claim(ClaimNames.Permission, "forms.read"));

        // Assert
        // forms.read (only once) + authenticated permissions should be present
        identity.Permissions.Should().HaveCount(2);
        identity.Permissions.Should().Contain("forms.read");
        identity.Permissions.Should().Contain(Actions.Access.Authenticated);
    }

    [Fact]
    public void IsAdmin_ReturnsFalse_WhenClaimMissing()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId: "123",
            tenantId: 1,
            roles: [],
            permissions: [],
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(5),
            eTag: string.Empty);

        // Act
        var identity = new AuthorizedIdentity(authData);
        var adminClaim = identity.FindFirst(ClaimNames.IsAdmin);
        adminClaim.Should().BeNull();

        // Assert
        identity.IsAdmin.Should().BeFalse();
    }
}

