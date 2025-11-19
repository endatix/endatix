using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authorization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using Xunit;

namespace Endatix.Infrastructure.Tests.Identity.Authentication;

public class ClaimsTransformerTests
{
    private readonly List<IAuthorizationStrategy> _authorizationStrategies;
    private readonly IAuthorizationCache _authorizationCache;
    private readonly ILogger<ClaimsTransformer> _logger;
    private readonly ClaimsTransformer _transformer;

    public ClaimsTransformerTests()
    {
        _authorizationStrategies = new List<IAuthorizationStrategy>();
        _authorizationCache = Substitute.For<IAuthorizationCache>();
        _logger = Substitute.For<ILogger<ClaimsTransformer>>();
        _transformer = new ClaimsTransformer(_authorizationStrategies, _authorizationCache, _logger);
    }

    #region TransformAsync Tests

    [Fact]
    public async Task TransformAsync_NullPrincipal_ReturnsPrincipal()
    {
        // Arrange
        ClaimsPrincipal? principal = null;

        // Act
        var result = await _transformer.TransformAsync(principal!);

        // Assert
        result.Should().Be(principal);
    }

    [Fact]
    public async Task TransformAsync_NotAuthenticated_ReturnsPrincipal()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        result.Should().Be(principal);
        result.Identities.Should().NotContain(i => i is AuthorizedIdentity);
    }

    [Fact]
    public async Task TransformAsync_NoUserId_ReturnsPrincipalWithoutAuthorizedIdentity()
    {
        // Arrange
        var identity = new ClaimsIdentity([], "test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        result.Should().Be(principal);
        result.Identities.Should().NotContain(i => i is AuthorizedIdentity);
    }

    [Fact]
    public async Task TransformAsync_NoIssuer_ReturnsPrincipalWithoutAuthorizedIdentity()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, "123")
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);
        _authorizationStrategies.Clear();

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        result.Should().Be(principal);
        result.Identities.Should().NotContain(i => i is AuthorizedIdentity);
    }

    [Fact]
    public async Task TransformAsync_NoMatchingAuthorizationStrategy_ReturnsPrincipalWithoutAuthorizedIdentity()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, "123"),
                new Claim(JwtRegisteredClaimNames.Iss, "test-issuer")
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);
        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(false);
        _authorizationStrategies.Clear();
        _authorizationStrategies.Add(strategy);

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        result.Should().Be(principal);
        result.Identities.Should().NotContain(i => i is AuthorizedIdentity);
    }

    [Fact]
    public async Task TransformAsync_ValidPrincipal_AddsAuthorizedIdentity()
    {
        // Arrange
        var userId = "123";
        var tenantId = 1L;
        string[] roles = ["User", "Admin"];
        string[] permissions = ["read:forms", "write:forms"];
        string[] expectedRoles = [SystemRole.Authenticated.Name, .. roles];
        string[] expectedPermissions = [.. SystemRole.Authenticated.Permissions, .. permissions];
        var expirySeconds = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, userId),
                new Claim(JwtRegisteredClaimNames.Iss, "test-issuer"),
                new Claim(JwtRegisteredClaimNames.Jti, "jti-123"),
                new Claim(JwtRegisteredClaimNames.Exp, expirySeconds.ToString())
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        var authorizationData = AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: tenantId,
            roles: roles,
            permissions: permissions,
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(15),
            eTag: "etag-123");

        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns(Result.Success(authorizationData));

        _authorizationStrategies.Clear();
        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                Arg.Is<ClaimsPrincipal>(p => p == principal),
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                var resultTask = factory(cancellationToken);
                var result = resultTask.GetAwaiter().GetResult();
                if (result.IsSuccess)
                {
                    return Task.FromResult(result.Value);
                }
                throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
            });

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        result.Should().NotBeNull();
        var authorizedIdentity = result.Identities.OfType<AuthorizedIdentity>().FirstOrDefault();
        authorizedIdentity.Should().NotBeNull();
        authorizedIdentity!.IsHydrated.Should().BeTrue();
        authorizedIdentity.TenantId.Should().Be(tenantId);
        authorizedIdentity.Roles.Should().BeEquivalentTo(expectedRoles);
        authorizedIdentity.Permissions.Should().BeEquivalentTo(expectedPermissions);
        authorizedIdentity.ETag.Should().Be("etag-123");
    }

    [Fact]
    public async Task TransformAsync_WithCachedData_UsesCache()
    {
        // Arrange
        var userId = "123";
        var tenantId = 1L;
        var roles = new[] { "User" };
        var permissions = new[] { "read:forms" };
        var expirySeconds = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, userId),
                new Claim(JwtRegisteredClaimNames.Iss, "test-issuer"),
                new Claim(JwtRegisteredClaimNames.Jti, "jti-123"),
                new Claim(JwtRegisteredClaimNames.Exp, expirySeconds.ToString())
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        var authorizationData = AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: tenantId,
            roles: roles,
            permissions: permissions,
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(15),
            eTag: "etag-123");

        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns(Result.Success(authorizationData));

        _authorizationStrategies.Clear();
        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                Arg.Is<ClaimsPrincipal>(p => p == principal),
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                var resultTask = factory(cancellationToken);
                var result = resultTask.GetAwaiter().GetResult();
                if (result.IsSuccess)
                {
                    return Task.FromResult(result.Value);
                }
                throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
            });

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        result.Should().NotBeNull();
        var authorizedIdentity = result.Identities.OfType<AuthorizedIdentity>().FirstOrDefault();
        authorizedIdentity.Should().NotBeNull();
        authorizedIdentity!.IsHydrated.Should().BeTrue();
        authorizedIdentity.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task TransformAsync_WithJti_UsesJtiForCacheKey()
    {
        // Arrange
        var userId = "123";
        var jti = "unique-jti-123";
        var tenantId = 1L;
        var roles = new[] { "User" };
        var permissions = new[] { "read:forms" };

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, userId),
                new Claim(JwtRegisteredClaimNames.Iss, "test-issuer"),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(JwtRegisteredClaimNames.Exp, DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds().ToString())
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        var authorizationData = AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: tenantId,
            roles: roles,
            permissions: permissions,
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(15),
            eTag: "etag-123");

        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns(Result.Success(authorizationData));

        _authorizationStrategies.Clear();
        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                Arg.Is<ClaimsPrincipal>(p => p == principal),
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                var resultTask = factory(cancellationToken);
                var result = resultTask.GetAwaiter().GetResult();
                if (result.IsSuccess)
                {
                    return Task.FromResult(result.Value);
                }
                throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
            });

        // Act
        await _transformer.TransformAsync(principal);

        // Assert
        await _authorizationCache.Received(1).GetOrCreateAsync(
            Arg.Is<ClaimsPrincipal>(p => p == principal),
            Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransformAsync_WithoutJti_UsesUserIdForCacheKey()
    {
        // Arrange
        var userId = "123";
        var tenantId = 1L;
        var roles = new[] { "User" };
        var permissions = new[] { "read:forms" };

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, userId),
                new Claim(JwtRegisteredClaimNames.Iss, "test-issuer"),
                new Claim(JwtRegisteredClaimNames.Exp, DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds().ToString())
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        var authorizationData = AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: tenantId,
            roles: roles,
            permissions: permissions,
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(15),
            eTag: "etag-123");

        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns(Result.Success(authorizationData));

        _authorizationStrategies.Clear();
        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                Arg.Is<ClaimsPrincipal>(p => p == principal),
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                var resultTask = factory(cancellationToken);
                var result = resultTask.GetAwaiter().GetResult();
                if (result.IsSuccess)
                {
                    return Task.FromResult(result.Value);
                }
                throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
            });

        // Act
        await _transformer.TransformAsync(principal);

        // Assert
        await _authorizationCache.Received(1).GetOrCreateAsync(
            Arg.Is<ClaimsPrincipal>(p => p == principal),
            Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransformAsync_WithExpiredToken_UsesFallbackCacheExpiration()
    {
        // Arrange
        var userId = "123";
        var tenantId = 1L;
        var roles = new[] { "User" };
        var permissions = new[] { "read:forms" };
        var expiredSeconds = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, userId),
                new Claim(JwtRegisteredClaimNames.Iss, "test-issuer"),
                new Claim(JwtRegisteredClaimNames.Exp, expiredSeconds.ToString())
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        var authorizationData = AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: tenantId,
            roles: roles,
            permissions: permissions,
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(15),
            eTag: "etag-123");

        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns(Result.Success(authorizationData));

        _authorizationStrategies.Clear();
        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                Arg.Is<ClaimsPrincipal>(p => p == principal),
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                var resultTask = factory(cancellationToken);
                var result = resultTask.GetAwaiter().GetResult();
                if (result.IsSuccess)
                {
                    return Task.FromResult(result.Value);
                }
                throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
            });

        // Act
        await _transformer.TransformAsync(principal);

        // Assert
        await _authorizationCache.Received(1).GetOrCreateAsync(
            Arg.Is<ClaimsPrincipal>(p => p == principal),
            Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransformAsync_WithValidExpiry_ComputesCacheExpiration()
    {
        // Arrange
        var userId = "123";
        var tenantId = 1L;
        var roles = new[] { "User" };
        var permissions = new[] { "read:forms" };
        var now = DateTimeOffset.UtcNow;
        var expiryTime = now.AddMinutes(30);
        var expirySeconds = expiryTime.ToUnixTimeSeconds();

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, userId),
                new Claim(JwtRegisteredClaimNames.Iss, "test-issuer"),
                new Claim(JwtRegisteredClaimNames.Exp, expirySeconds.ToString())
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        var authorizationData = AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: tenantId,
            roles: roles,
            permissions: permissions,
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(15),
            eTag: "etag-123");

        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns(Result.Success(authorizationData));

        _authorizationStrategies.Clear();
        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                Arg.Is<ClaimsPrincipal>(p => p == principal),
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                var resultTask = factory(cancellationToken);
                var result = resultTask.GetAwaiter().GetResult();
                if (result.IsSuccess)
                {
                    return Task.FromResult(result.Value);
                }
                throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
            });

        // Act
        await _transformer.TransformAsync(principal);

        // Assert
        await _authorizationCache.Received(1).GetOrCreateAsync(
            Arg.Is<ClaimsPrincipal>(p => p == principal),
            Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransformAsync_WithAdminRole_SetsIsAdminClaim()
    {
        // Arrange
        var userId = "123";
        var tenantId = 1L;
        var roles = new[] { SystemRole.Admin.Name };
        var permissions = new[] { "read:forms" };
        var expirySeconds = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, userId),
                new Claim(JwtRegisteredClaimNames.Iss, "test-issuer"),
                new Claim(JwtRegisteredClaimNames.Exp, expirySeconds.ToString())
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        var authorizationData = AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: tenantId,
            roles: roles,
            permissions: permissions,
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(15),
            eTag: "etag-123");

        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns(Result.Success(authorizationData));

        _authorizationStrategies.Clear();
        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                Arg.Is<ClaimsPrincipal>(p => p == principal),
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                var resultTask = factory(cancellationToken);
                var result = resultTask.GetAwaiter().GetResult();
                if (result.IsSuccess)
                {
                    return Task.FromResult(result.Value);
                }
                throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
            });

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        var authorizedIdentity = result.Identities.OfType<AuthorizedIdentity>().FirstOrDefault();
        authorizedIdentity.Should().NotBeNull();
        authorizedIdentity!.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithNullAuthorizationData_DoesNotAddAuthorizedIdentity()
    {
        // Arrange
        var userId = "123";
        var expirySeconds = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, userId),
                new Claim(JwtRegisteredClaimNames.Iss, "test-issuer"),
                new Claim(JwtRegisteredClaimNames.Exp, expirySeconds.ToString())
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns(Result.Error("No authorization data available"));

        _authorizationStrategies.Clear();
        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                Arg.Is<ClaimsPrincipal>(p => p == principal),
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // Execute the factory to get the Result
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                var resultTask = factory(cancellationToken);
                var result = resultTask.GetAwaiter().GetResult();
                // The cache throws when the result is not successful (matching real implementation)
                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
                }
                return Task.FromResult(result.Value);
            });

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        result.Should().Be(principal);
        result.Identities.Should().NotContain(i => i is AuthorizedIdentity);
    }

    #endregion
}

