using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity;
using NSubstitute;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public class PermissionsHandlerTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _dbContext;
    private readonly IUserContext _userContext;
    private readonly PermissionsHandler _handler;

    public PermissionsHandlerTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _cache = Substitute.For<IMemoryCache>();
        _dbContext = Substitute.For<AppDbContext>();
        _userContext = Substitute.For<IUserContext>();

        _handler = new PermissionsHandler(_httpContextAccessor, _cache, _dbContext, _userContext);
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
        var context = CreateAuthorizationContext(isAdmin: true);

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
    public async Task HandleAsync_NoEndpointMetadata_FailsAuthorization()
    {
        // Arrange
        var httpContext = Substitute.For<HttpContext>();
        _httpContextAccessor.HttpContext.Returns(httpContext);
        httpContext.GetEndpoint().Returns((Endpoint?)null);

        var context = CreateAuthorizationContext();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasFailed.Should().BeTrue();
    }

    private AuthorizationHandlerContext CreateAuthorizationContext(bool isAdmin = false)
    {
        var claims = new List<Claim>();

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimNames.IsAdmin, "true"));
        }

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var requirements = new[] { new TestRequirement() };
        return new AuthorizationHandlerContext(requirements, user, null);
    }

    private class TestRequirement : IAuthorizationRequirement
    {
    }
}