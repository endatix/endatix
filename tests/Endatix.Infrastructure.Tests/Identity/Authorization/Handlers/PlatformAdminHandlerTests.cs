using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Authorization.Handlers;
using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Tests.Identity.Authorization.Handlers;

public sealed class PlatformAdminHandlerTests
{
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly PlatformAdminHandler _handler;

    public PlatformAdminHandlerTests()
    {
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _handler = new PlatformAdminHandler(_authorizationService);
    }

    [Fact]
    public async Task HandleRequirementAsync_NoUser_ReturnsEarly()
    {
        // Arrange
        var context = CreateAuthorizationContext(user: null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
        await _authorizationService.DidNotReceive().IsPlatformAdminAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequirementAsync_HydratedAndPlatformAdmin_Succeeds()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isHydrated: true, isPlatformAdmin: true);
        var context = CreateAuthorizationContext(user: user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
        await _authorizationService.DidNotReceive()
            .IsPlatformAdminAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequirementAsync_NotHydratedButPlatformAdmin_Succeeds()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isHydrated: false, isPlatformAdmin: false);
        _authorizationService.IsPlatformAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(true));
        var context = CreateAuthorizationContext(user: user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
        await _authorizationService.Received(1)
            .IsPlatformAdminAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleRequirementAsync_NotPlatformAdminAndNotHydrated_Fails()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isHydrated: false, isPlatformAdmin: false);
        _authorizationService.IsPlatformAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        var context = CreateAuthorizationContext(user: user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        await _authorizationService.Received(1)
            .IsPlatformAdminAsync(Arg.Any<CancellationToken>());
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_ServiceReturnsError_Fails()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isHydrated: false, isPlatformAdmin: false);
        _authorizationService.IsPlatformAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Error("Service error"));
        var context = CreateAuthorizationContext(user: user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        await _authorizationService.Received(1)
            .IsPlatformAdminAsync(Arg.Any<CancellationToken>());
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_HydratedButNotPlatformAdminRole_Fails()
    {
        // Arrange
        var user = CreateClaimsPrincipal(userId: "123", isHydrated: true, isPlatformAdmin: false);
        _authorizationService.IsPlatformAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        var context = CreateAuthorizationContext(user: user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        await _authorizationService.DidNotReceive()
            .IsPlatformAdminAsync(Arg.Any<CancellationToken>());
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeFalse();
    }

    #region Helper Methods

    private ClaimsPrincipal? CreateClaimsPrincipal(string? userId, bool isHydrated, bool isPlatformAdmin)
    {
        if (userId is null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<Claim>
        {
            new(ClaimNames.UserId, userId)
        };

        if (isHydrated)
        {
            claims.Add(new Claim(ClaimNames.Hydrated, "true"));
        }

        if (isPlatformAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, SystemRole.PlatformAdmin.Name));
        }

        var identity = new ClaimsIdentity(claims, "test");
        return new ClaimsPrincipal(identity);
    }

    private AuthorizationHandlerContext CreateAuthorizationContext(ClaimsPrincipal? user)
    {
        var requirements = new[] { new PlatformAdminRequirement() };
        return new AuthorizationHandlerContext(requirements, user ?? new ClaimsPrincipal(), null);
    }

    #endregion
}

