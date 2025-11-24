using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Infrastructure.Tests.Identity;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void IsHydrated_ReturnsFalse_WhenNotAuthenticated()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act and Assert
        principal.IsHydrated().Should().BeFalse();
    }

    [Fact]
    public void IsHydrated_ReturnsTrue_WhenAuthorizedIdentityPresent()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            "123",
            1,
            [],
            [],
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(5),
            eTag: string.Empty);

        var mainIdentity = new ClaimsIdentity("auth-type");
        var principal = new ClaimsPrincipal(mainIdentity);
        principal.AddIdentity(new AuthorizedIdentity(authData));

        // Act and Assert
        principal.IsHydrated().Should().BeTrue();
    }

    [Fact]
    public void IsHydrated_ReturnsTrue_WhenHydratedClaimPresent()
    {
        // Arrange
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimNames.Hydrated, "true")
        ], "test");
        var principal = new ClaimsPrincipal(identity);

        // Act and Assert
        principal.IsHydrated().Should().BeTrue();
    }

    [Fact]
    public void GetUserId_ReturnsClaimValue()
    {
        // Arrange
        var userId = "user-123";
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimNames.UserId, userId)
        ], "test");
        var principal = new ClaimsPrincipal(identity);

        // Act and Assert
        principal.GetUserId().Should().Be(userId);
    }

    [Fact]
    public void GetUserId_FallsBackToNameIdentifier()
    {
        // Arrange
        var userId = "fallback-456";
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId)
        ], "test");
        var principal = new ClaimsPrincipal(identity);

        // Act and Assert
        principal.GetUserId().Should().Be(userId);
    }

    [Fact]
    public void GetTenantId_ReturnsDefault_WhenUnauthenticated()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act and Assert
        principal.GetTenantId().Should().Be(AuthConstants.DEFAULT_TENANT_ID);
    }

    [Fact]
    public void GetTenantId_ReturnsAuthorizedIdentityTenant()
    {
        // Arrange
        var authData = AuthorizationData.ForAuthenticatedUser(
            "123",
            tenantId: 99,
            [],
            [],
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(5),
            eTag: string.Empty);
        var principal = new ClaimsPrincipal(new[] { new ClaimsIdentity("auth-type"), new AuthorizedIdentity(authData) });

        // Act and Assert
        principal.GetTenantId().Should().Be(99);
    }

    [Fact]
    public void GetIssuer_ReturnsIssClaim()
    {
        // Arrange
        var issuer = "https://issuer";
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "123"),
            new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Iss, issuer)
        ], "test");
        var principal = new ClaimsPrincipal(identity);

        // Act and Assert
        principal.GetIssuer().Should().Be(issuer);
    }

    [Fact]
    public void IsAdmin_ReturnsSuccess_WhenHydrated()
    {
        var authData = AuthorizationData.ForAuthenticatedUser(
            "123",
            1,
            [SystemRole.Admin.Name],
            [],
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(5),
            eTag: string.Empty);
        var principal = new ClaimsPrincipal(new[] { new ClaimsIdentity("auth-type"), new AuthorizedIdentity(authData) });

        var result = principal.IsAdmin();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_ReturnsError_WhenNotHydrated()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimNames.UserId, "123")
        ], "test");
        var principal = new ClaimsPrincipal(identity);

        var result = principal.IsAdmin();

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Claims principal is not hydrated");
    }
}

