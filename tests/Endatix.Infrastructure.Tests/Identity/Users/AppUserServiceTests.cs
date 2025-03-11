using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Users;
using FluentAssertions;
using System.Security.Claims;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Tests;

namespace Endatix.Infrastructure.Tests.Identity.Users;

public class AppUserServiceTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppUserService _userService;

    public AppUserServiceTests()
    {
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null, null, null, null, null, null, null, null);
        _userService = new AppUserService(_userManager);
    }

    [Fact]
    public async Task GetUserAsync_NullClaimsPrincipal_ReturnsNotFound()
    {
        // Act
        ClaimsPrincipal? nullClaimsPrincipal = null;
        var result = await _userService.GetUserAsync(nullClaimsPrincipal!);

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetUserAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _userManager.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(null as AppUser);

        // Act
        var result = await _userService.GetUserAsync(claimsPrincipal);

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetUserAsync_UserFound_ReturnsSuccess()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var appUser = new AppUser { 
            Id = 22_111_111_111_111_111,
            TenantId = 1,
            UserName = "test@example.com",
            Email = "test@example.com"
        };
        _userManager.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(appUser);

        // Act
        var result = await _userService.GetUserAsync(claimsPrincipal);

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(appUser.Id);
        result.Value.TenantId.Should().Be(appUser.TenantId);
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ReturnsError()
    {
        // Arrange
        var user = new User(22_111_111_111_111_111, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(null as AppUser);

        // Act
        var result = await _userService.ChangePasswordAsync(user, "currentPass", "newPass");

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.Error);
        result.Errors.Should().Contain("User not found");
    }

    [Fact]
    public async Task ChangePasswordAsync_EmptyCurrentPassword_ReturnsError()
    {
        // Arrange
        var user = new User(22_111_111_111_111_111, SampleData.TENANT_ID, "test@example.com", "password", true);
        var appUser = new AppUser { 
            Id = user.Id, 
            UserName = user.Email,
            Email = user.Email,
        };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(appUser);

        // Act
        var result = await _userService.ChangePasswordAsync(user, "", "newPass");

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.Error);
        result.Errors.Should().Contain(error => error.Contains("current password is required"));
    }

    [Fact]
    public async Task ChangePasswordAsync_ChangePasswordFails_ReturnsError()
    {
        // Arrange
        var user = new User(22_111_111_111_111_111, SampleData.TENANT_ID, "test@example.com", "password", true);
        var appUser = new AppUser { Id = user.Id, UserName = user.Email };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(appUser);
        _userManager.ChangePasswordAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Password change failed" }));

        // Act
        var result = await _userService.ChangePasswordAsync(user, "currentPass", "newPass");

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.Error);
        result.Errors.Should().Contain("Password change failed");
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_ReturnsSuccessMessage()
    {
        // Arrange
        var user = new User(22_111_111_111_111_111, SampleData.TENANT_ID, "test@example.com", "password", true);
        var appUser = new AppUser { Id = user.Id, UserName = user.Email };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(appUser);
        _userManager.ChangePasswordAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        // Act
        var result = await _userService.ChangePasswordAsync(user, "currentPass", "newPass");

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.Ok);
        result.Value.Should().Be("Password changed successfully");
    }
}
