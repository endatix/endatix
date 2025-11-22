using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Authorization.Handlers;
using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Tests.Identity.Authorization.Handlers;

public sealed class TenantAdminHandlerTests
{
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly TenantAdminHandler _handler;

    public TenantAdminHandlerTests()
    {
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _handler = new TenantAdminHandler(_authorizationService);
    }


    [Fact]
    public async Task HandleRequirementAsync_NoUser_ReturnsEarly()
    {
        // Arrange
        var context = CreateAuthorizationContext(user: null);
        var requirement = new TenantAdminRequirement();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
        await _authorizationService.DidNotReceive().IsAdminAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequirementAsync_HydratedAndAdmin_Succeeds()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isAdmin: true, isHydrated: true);
        var context = CreateAuthorizationContext(user: user);
        var requirement = new TenantAdminRequirement();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
        await _authorizationService.DidNotReceive().IsAdminAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequirementAsync_NotHydratedButAdmin_Succeeds()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false, isHydrated: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(true));
        var context = CreateAuthorizationContext(user: user);
        var requirement = new TenantAdminRequirement();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
        await _authorizationService.Received(1).IsAdminAsync(Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task HandleRequirementAsync_NotAdminAndNotHydrated_Fails()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false, isHydrated: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        var context = CreateAuthorizationContext(user: user);
        var requirement = new TenantAdminRequirement();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
        await _authorizationService.Received(1).IsAdminAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequirementAsync_HydratedButNotAdmin_Fails()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false, isHydrated: true);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        var context = CreateAuthorizationContext(user: user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
        await _authorizationService.DidNotReceive().IsAdminAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequirementAsync_AdminAndNotHydrated_Fails()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isAdmin: true, isHydrated: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(true));
        var context = CreateAuthorizationContext(user: user);
    }


    [Fact]
    public async Task HandleRequirementAsync_ServiceReturnsError_Fails()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isAdmin: false, isHydrated: false);
        _authorizationService.IsAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Error("Service error"));
        var context = CreateAuthorizationContext(user: user);
        var requirement = new TenantAdminRequirement();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
        await _authorizationService.Received(1).IsAdminAsync(Arg.Any<CancellationToken>());
    }

    #region Helper Methods

    private ClaimsPrincipal? CreateClaimsPrincipal(string? userId, bool isAdmin, bool isHydrated)
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
        }

        if (isHydrated)
        {
            claims.Add(new Claim(ClaimNames.Hydrated, "true"));
        }

        var identity = new ClaimsIdentity(claims, "test");
        return new ClaimsPrincipal(identity);
    }

    private AuthorizationHandlerContext CreateAuthorizationContext(ClaimsPrincipal? user)
    {
        var requirements = new[] { new TenantAdminRequirement() };
        return new AuthorizationHandlerContext(requirements, user ?? new ClaimsPrincipal(), null);
    }

    #endregion
}

