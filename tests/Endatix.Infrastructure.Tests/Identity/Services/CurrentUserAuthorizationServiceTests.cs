using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Identity.Services;

public sealed class CurrentUserAuthorizationServiceTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationCache _authorizationCache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CurrentUserAuthorizationService> _logger;
    private readonly List<IAuthorizationStrategy> _authorizationStrategies;
    private readonly CurrentUserAuthorizationService _service;

    private static AuthorizationData CreateAuthorizationData(
        string userId,
        long tenantId,
        string[] roles,
        string[] permissions) =>
        AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: tenantId,
            roles: roles,
            permissions: permissions,
            cachedAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(5),
            eTag: "test-etag");

    private void SetupHydratedPrincipal(AuthorizationData authorizationData)
    {
        var originalIdentity = new ClaimsIdentity(
            [
                new Claim(ClaimNames.UserId, authorizationData.UserId)
            ],
            "test");
        var authorizedIdentity = new AuthorizedIdentity(authorizationData);
        var principal = new ClaimsPrincipal(new[] { originalIdentity, authorizedIdentity });
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        _httpContextAccessor.HttpContext.Returns(httpContext);
    }

    private void SetupPrincipal(ClaimsPrincipal principal)
    {
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        _httpContextAccessor.HttpContext.Returns(httpContext);
    }

    public CurrentUserAuthorizationServiceTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _authorizationCache = Substitute.For<IAuthorizationCache>();
        _tenantContext = Substitute.For<ITenantContext>();
        _authorizationStrategies = new List<IAuthorizationStrategy>();
        _logger = Substitute.For<ILogger<CurrentUserAuthorizationService>>();

        _service = new CurrentUserAuthorizationService(
            _httpContextAccessor,
            _authorizationCache,
            _authorizationStrategies,
            _tenantContext,
            _logger);
    }

    #region GetAuthorizationDataAsync Tests

    [Fact]
    public async Task GetAuthorizationDataAsync_NullHttpContext_ReturnsError()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        // Act
        var result = await _service.GetAuthorizationDataAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsError().Should().BeTrue();
        result.Errors.First().Should().Contain("No current user found");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_NoUser_ReturnsError()
    {
        // Arrange
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns((ClaimsPrincipal?)null);
        _httpContextAccessor.HttpContext.Returns(httpContext);

        // Act
        var result = await _service.GetAuthorizationDataAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsError().Should().BeTrue();
        result.Errors.First().Should().Contain("No current user found");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_UnauthenticatedUser_ReturnsAnonymousUser()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        _httpContextAccessor.HttpContext.Returns(httpContext);
        _tenantContext.TenantId.Returns(1L);

        // Act
        var result = await _service.GetAuthorizationDataAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be("anonymous");
        result.Value.Roles.Should().Contain(SystemRole.Public.Name);
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_NoIdentity_ReturnsAnonymousUser()
    {
        // Arrange
        // Principal with no identity means no userId, so it returns anonymous user
        var principal = new ClaimsPrincipal(); // No identity
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        _httpContextAccessor.HttpContext.Returns(httpContext);
        _tenantContext.TenantId.Returns(1L);

        // Act
        var result = await _service.GetAuthorizationDataAsync(CancellationToken.None);

        // Assert
        // When there's no identity, GetUserId returns null, so it returns anonymous user
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be("anonymous");
        result.Value.Roles.Should().Contain(SystemRole.Public.Name);
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_RegularClaimsIdentity_NotHydrated_NoStrategy_ReturnsError()
    {
        // Arrange
        var userId = "123";
        var claims = new List<Claim> { new(ClaimNames.UserId, userId) };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        _httpContextAccessor.HttpContext.Returns(httpContext);

        // Act
        var result = await _service.GetAuthorizationDataAsync(CancellationToken.None);

        // Assert
        // ExtractAuthorizationData returns NotFound, so GetAuthorizationDataAsync continues and tries to find an authorization strategy. Since no strategy is configured, it returns error.
        result.IsSuccess.Should().BeFalse();
        result.IsError().Should().BeTrue();
        result.Errors.First().Should().Contain("No authorization provider found");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_WithAuthorizedIdentity_Succeeds()
    {
        // Arrange
        var userId = "123";
        var tenantId = 1L;
        var roles = new[] { SystemRole.Admin.Name };
        var permissions = new[] { "read:forms" };
        var cachedAt = DateTime.UtcNow;
        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        var eTag = "etag-123";

        var authorizationData = AuthorizationData.ForAuthenticatedUser(
            userId: userId,
            tenantId: tenantId,
            roles: roles,
            permissions: permissions,
            cachedAt: cachedAt,
            expiresAt: expiresAt,
            eTag: eTag);

        // In real scenarios, the principal has both:
        // 1. The original identity with userId (from JWT/session)
        // 2. The AuthorizedIdentity added by the claims transformer
        var originalIdentity = new ClaimsIdentity(
            new List<Claim> { new(ClaimNames.UserId, userId) },
            "test");
        var authorizedIdentity = new AuthorizedIdentity(authorizationData);
        var principal = new ClaimsPrincipal(new[] { originalIdentity, authorizedIdentity });
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        _httpContextAccessor.HttpContext.Returns(httpContext);

        // Act
        var result = await _service.GetAuthorizationDataAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.Roles.Should().Contain(SystemRole.Admin.Name);
        result.Value.Permissions.Should().Contain("read:forms");
        result.Value.IsAdmin.Should().BeTrue();
        result.Value.ETag.Should().Be(eTag);
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_WithUserIdAndStrategy_Succeeds()
    {
        // Arrange
        var userId = "123";
        var tenantId = 1L;
        var roles = new[] { "User" };
        var permissions = new[] { "read:forms" };

        var claims = new List<Claim> { new(ClaimNames.UserId, userId) };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        _httpContextAccessor.HttpContext.Returns(httpContext);

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

        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                principal,
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                return factory(cancellationToken).Result.Value!;
            });

        // Act
        var result = await _service.GetAuthorizationDataAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_StrategyThrowsException_ReturnsError()
    {
        // Arrange
        var userId = "123";
        var claims = new List<Claim> { new(ClaimNames.UserId, userId) };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        _httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns<Result<AuthorizationData>>(x => throw new Exception("Strategy error"));

        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                principal,
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                var result = await factory(cancellationToken);
                return result.Value!;
            });

        // Act
        var result = await _service.GetAuthorizationDataAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsError().Should().BeTrue();
        result.Errors.First().Should().Contain("Failed to retrieve user role information");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_StrategyReturnsError_ResultIsError()
    {
        // Arrange
        var userId = "123";
        var claims = new List<Claim> { new(ClaimNames.UserId, userId) };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        _httpContextAccessor.HttpContext.Returns(httpContext);

        var strategy = Substitute.For<IAuthorizationStrategy>();
        strategy.CanHandle(principal).Returns(true);
        strategy.GetAuthorizationDataAsync(principal, Arg.Any<CancellationToken>())
            .Returns(Result<AuthorizationData>.Error("identity store failure"));

        _authorizationStrategies.Add(strategy);

        _authorizationCache
            .GetOrCreateAsync(
                principal,
                Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, Task<Result<AuthorizationData>>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                var result = await factory(cancellationToken);
                throw new InvalidOperationException($"Authorization data factory returned status {result.Status}");
            });


        // Act
        var result = await _service.GetAuthorizationDataAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsError().Should().BeTrue();
        result.Errors.First().Should().Contain("Failed to retrieve user role information");
    }

    #endregion

    #region Permission helper tests

    [Fact]
    public async Task HasPermissionAsync_AdminUser_ReturnsTrue()
    {
        var authorizationData = CreateAuthorizationData(
            "123",
            1,
            [SystemRole.Admin.Name],
            []);
        SetupHydratedPrincipal(authorizationData);

        var result = await _service.HasPermissionAsync("any:permission", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_NonAdmin_EvaluatesPermissionSet()
    {
        var authorizationData = CreateAuthorizationData(
            "123",
            1,
            ["User"],
            ["read:forms"]);
        SetupHydratedPrincipal(authorizationData);

        var allowed = await _service.HasPermissionAsync("read:forms", CancellationToken.None);
        var denied = await _service.HasPermissionAsync("write:forms", CancellationToken.None);

        allowed.IsSuccess.Should().BeTrue();
        allowed.Value.Should().BeTrue();
        denied.IsSuccess.Should().BeTrue();
        denied.Value.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionsAsync_AdminUser_ReturnsTrueForAll()
    {
        var authorizationData = CreateAuthorizationData(
            "123",
            1,
            [SystemRole.Admin.Name],
            []);
        SetupHydratedPrincipal(authorizationData);

        var permissions = new[] { "read:forms", "write:forms" };
        var result = await _service.HasPermissionsAsync(permissions, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(permissions.ToDictionary(p => p, _ => true));
    }

    [Fact]
    public async Task HasPermissionsAsync_NonAdmin_ReturnsPerPermissionFlags()
    {
        var authorizationData = CreateAuthorizationData(
            "123",
            1,
            ["User"],
            ["read:forms"]);
        SetupHydratedPrincipal(authorizationData);

        var permissions = new[] { "read:forms", "write:forms" };
        var result = await _service.HasPermissionsAsync(permissions, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainKey("read:forms").WhoseValue.Should().BeTrue();
        result.Value.Should().ContainKey("write:forms").WhoseValue.Should().BeFalse();
    }

    [Fact]
    public async Task IsAdminAsync_ReturnsFlagFromAuthorizedIdentity()
    {
        var authorizationData = CreateAuthorizationData(
            "123",
            1,
            [SystemRole.Admin.Name],
            []);
        SetupHydratedPrincipal(authorizationData);

        var result = await _service.IsAdminAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task IsPlatformAdminAsync_ReturnsFlagFromAuthorizedIdentity()
    {
        var authorizationData = CreateAuthorizationData(
            "123",
            1,
            [SystemRole.PlatformAdmin.Name],
            []);
        SetupHydratedPrincipal(authorizationData);

        var result = await _service.IsPlatformAdminAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAccessAsync_AdminBypassesPermissionCheck()
    {
        var authorizationData = CreateAuthorizationData(
            "123",
            1,
            [SystemRole.Admin.Name],
            []);
        SetupHydratedPrincipal(authorizationData);

        var result = await _service.ValidateAccessAsync("any:permission", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAccessAsync_WithPermission_ReturnsSuccess()
    {
        var authorizationData = CreateAuthorizationData(
            "123",
            1,
            ["User"],
            ["read:forms"]);
        SetupHydratedPrincipal(authorizationData);

        var result = await _service.ValidateAccessAsync("read:forms", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAccessAsync_WithoutPermission_ReturnsForbidden()
    {
        var authorizationData = CreateAuthorizationData(
            "123",
            1,
            ["User"],
            []);
        SetupHydratedPrincipal(authorizationData);

        var result = await _service.ValidateAccessAsync("write:forms", CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    #endregion

    #region Cache invalidation tests

    [Fact]
    public async Task InvalidateAuthorizationDataCacheAsync_WithUserId_InvokesCache()
    {
        var authorizationData = CreateAuthorizationData(
            "123",
            42,
            ["User"],
            []);
        SetupHydratedPrincipal(authorizationData);
        _tenantContext.TenantId.Returns(authorizationData.TenantId);

        await _service.InvalidateAuthorizationDataCacheAsync(CancellationToken.None);

        await _authorizationCache.Received(1).InvalidateAsync(authorizationData.UserId, authorizationData.TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAuthorizationDataCacheAsync_NoUserId_DoesNotInvokeCache()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        SetupPrincipal(principal);

        await _service.InvalidateAuthorizationDataCacheAsync(CancellationToken.None);

        await _authorizationCache.DidNotReceive().InvalidateAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    #endregion
}
