using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization.Handlers;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Tests.Identity.Authorization.Handlers;

public sealed class AssertionPermissionsHandlerTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HybridCache _cache;
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly AssertionPermissionsHandler _handler;

    public AssertionPermissionsHandlerTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _cache = Substitute.For<HybridCache>();
        _dbContext = Substitute.For<AppDbContext>();
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _handler = new AssertionPermissionsHandler(_httpContextAccessor, _cache, _dbContext, _authorizationService);
    }

    [Fact]
    public async Task HandleRequirementAsync_NoUser_ReturnsEarly()
    {
        // Arrange
        var context = CreateAuthorizationContext(user: null);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_AdminUserViaClaims_Succeeds()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isAdmin: true);
        var context = CreateAuthorizationContext(user: user);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_AdminUserViaService_Succeeds()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(true));

        var context = CreateAuthorizationContext(user: user);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_AdminUserViaService_InvalidUserId_ReturnsEarly()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: null, isAdmin: false);
        var context = CreateAuthorizationContext(user: user);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_AdminUserViaService_NonNumericUserId_ReturnsEarly()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "invalid", isAdmin: false);
        var context = CreateAuthorizationContext(user: user);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoHttpContext_ReturnsEarly()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        var context = CreateAuthorizationContext(user: user);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoEndpoint_ReturnsEarly()
    {
        // Arrange
        var httpContext = CreateHttpContext();
        httpContext.GetEndpoint().Returns((Endpoint?)null);
        _httpContextAccessor.HttpContext.Returns(httpContext);

        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        var context = CreateAuthorizationContext(user: user);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoEndpointDefinitionMetadata_ReturnsEarly()
    {
        // Arrange
        var httpContext = CreateHttpContext();
        // Create metadata collection without EndpointDefinition - this simulates a non-FastEndpoints endpoint
        var metadata = new Microsoft.AspNetCore.Http.EndpointMetadataCollection();
        var endpoint = new Microsoft.AspNetCore.Http.Endpoint(
            _ => Task.CompletedTask,
            metadata,
            "test-endpoint");
        httpContext.GetEndpoint().Returns(endpoint);
        _httpContextAccessor.HttpContext.Returns(httpContext);

        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        var context = CreateAuthorizationContext(user: user);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoAllowedPermissions_ReturnsEarly()
    {
        // Arrange
        SetupHttpContextWithEndpoint(allowedPermissions: null);

        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        var context = CreateAuthorizationContext(user: user);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_EmptyAllowedPermissions_ReturnsEarly()
    {
        // Arrange
        SetupHttpContextWithEndpoint(allowedPermissions: Array.Empty<string>());

        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        var context = CreateAuthorizationContext(user: user);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoOwnerPermissions_ReturnsEarly()
    {
        // Arrange
        SetupHttpContextWithEndpoint(allowedPermissions: new[] { "permission.read", "permission.write" });

        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        var context = CreateAuthorizationContext(user: user);
        var requirement = new AssertionRequirement(_ => Task.FromResult(true));

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    // Note: Owner permission tests that require real FastEndpoints endpoint instances,
    // database access, and cache interactions have been removed as they are integration tests.
    // These should be tested in integration test projects with proper test infrastructure.

    #region Helper Methods

    private ClaimsPrincipal? CreateClaimsPrincipal(string? userId, bool isAdmin)
    {
        if (userId is null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<Claim>
        {
            new(ClaimNames.UserId, userId)
        };

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimNames.IsAdmin, "true"));
            claims.Add(new Claim(ClaimNames.Hydrated, "true"));
        }

        var identity = new ClaimsIdentity(claims, "test");
        return new ClaimsPrincipal(identity);
    }

    private AuthorizationHandlerContext CreateAuthorizationContext(ClaimsPrincipal? user)
    {
        var requirements = new[] { new AssertionRequirement(_ => Task.FromResult(true)) };
        return new AuthorizationHandlerContext(requirements, user ?? new ClaimsPrincipal(), null);
    }

    private HttpContext CreateHttpContext()
    {
        var httpContext = Substitute.For<HttpContext>();
        var request = Substitute.For<HttpRequest>();
        var routeValues = new Microsoft.AspNetCore.Routing.RouteValueDictionary();
        request.RouteValues.Returns(routeValues);
        httpContext.Request.Returns(request);
        return httpContext;
    }

    private void SetupHttpContextWithEndpoint(
        IList<string>? allowedPermissions = null)
    {
        var httpContext = CreateHttpContext();

        // Create a real FastEndpoints endpoint and extract its EndpointDefinition
        // This is needed because EndpointDefinition is sealed and cannot be mocked
        var endpointInstance = Factory.Create<TestEndpointWithoutEntity>(allowedPermissions);
        var endpointDefinition = endpointInstance.Definition;

        var metadata = new Microsoft.AspNetCore.Http.EndpointMetadataCollection(endpointDefinition);
        var endpoint = new Microsoft.AspNetCore.Http.Endpoint(
            _ => Task.CompletedTask,
            metadata,
            "test-endpoint");
        httpContext.GetEndpoint().Returns(endpoint);

        _httpContextAccessor.HttpContext.Returns(httpContext);
    }

    private sealed class TestEndpointWithoutEntity : Endpoint<object, object>
    {
        private readonly IList<string>? _allowedPermissions;

        public TestEndpointWithoutEntity(IList<string>? allowedPermissions = null)
        {
            _allowedPermissions = allowedPermissions;
        }

        public override void Configure()
        {
            Get("test");
            if (_allowedPermissions is not null && _allowedPermissions.Count > 0)
            {
                Permissions(_allowedPermissions.ToArray());
            }
        }

        public override Task<object> ExecuteAsync(object req, CancellationToken ct)
        {
            return Task.FromResult<object>(new { });
        }
    }

    #endregion
}