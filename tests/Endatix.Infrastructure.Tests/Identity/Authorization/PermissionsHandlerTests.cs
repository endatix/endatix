using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity.Authorization;
using NSubstitute;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public class PermissionsHandlerTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HybridCache _cache;
    private readonly AppDbContext _dbContext;
    private readonly IUserContext _userContext;
    private readonly IPermissionService _permissionService;
    private readonly PermissionsHandler _handler;

    public PermissionsHandlerTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _cache = Substitute.For<HybridCache>();
        _dbContext = Substitute.For<AppDbContext>();
        _userContext = Substitute.For<IUserContext>();
        _permissionService = Substitute.For<IPermissionService>();

        _handler = new PermissionsHandler(_httpContextAccessor, _cache, _dbContext, _userContext, _permissionService);
    }

    [Fact]
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

    [Fact]
    public async Task HandleAsync_AdminUser_SucceedsAllRequirements()
    {
        // Arrange
        var userId = "123";
        _userContext.GetCurrentUserId().Returns(userId);
        _permissionService.IsUserAdminAsync(123).Returns(Result.Success(true));

        var context = CreateAuthorizationContext();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
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

    [Fact]
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