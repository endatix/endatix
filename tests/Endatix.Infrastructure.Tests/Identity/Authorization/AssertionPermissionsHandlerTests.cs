using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Microsoft.Extensions.Caching.Hybrid;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization.Handlers;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public class AssertionPermissionsHandlerTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HybridCache _cache;
    private readonly AppDbContext _dbContext;
    private readonly IPermissionService _permissionService;
    private readonly AssertionPermissionsHandler _handler;

    public AssertionPermissionsHandlerTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _cache = Substitute.For<HybridCache>();
        _dbContext = Substitute.For<AppDbContext>();
        _permissionService = Substitute.For<IPermissionService>();

        _handler = new AssertionPermissionsHandler(_httpContextAccessor, _cache, _dbContext, _permissionService);
    }

    [Fact(Skip = "Skipping until next PR")]
    public async Task HandleAsync_AlreadySucceeded_DoesNothing()
    {
        // Arrange
        var context = CreateAuthorizationContext();
        context.Succeed(context.Requirements.First());

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact(Skip = "Skipping until next PR")]
    public async Task HandleAsync_AdminUser_SucceedsAllRequirements()
    {
        // Arrange
        var userId = "123";
        _permissionService.IsUserAdminAsync(123).Returns(Result.Success(true));

        var context = CreateAuthorizationContext();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact(Skip = "Skipping until next PR")]
    public async Task HandleAsync_NoHttpContext_DoesNothing()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        var context = CreateAuthorizationContext();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact(Skip = "Skipping until next PR")]
    public async Task HandleAsync_NoEndpointMetadata_DoesNothing()
    {
        // Arrange
        var httpContext = Substitute.For<HttpContext>();
        _httpContextAccessor.HttpContext.Returns(httpContext);
        httpContext.GetEndpoint().Returns((Endpoint?)null);

        var context = CreateAuthorizationContext();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    private AuthorizationHandlerContext CreateAuthorizationContext()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        var requirements = new[] { new TestRequirement() };
        return new AuthorizationHandlerContext(requirements, user, null);
    }

    private class TestRequirement : IAuthorizationRequirement
    {
    }
}