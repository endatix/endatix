using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.MyAccount.ChangePassword;
using System.Security.Claims;

namespace Endatix.Core.Tests.UseCases.MyAccount.ChangePassword;

public class ChangePasswordHandlerTests
{
    private readonly IUserService _userService;
    private readonly ChangePasswordHandler _handler;
    private readonly ClaimsPrincipal _claimsPrincipal;

    public ChangePasswordHandlerTests()
    {
        _userService = Substitute.For<IUserService>();
        _handler = new ChangePasswordHandler(_userService);
        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsInvalidResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_claimsPrincipal, "currentPass", "newPass");
        _userService.GetUserAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Result<User>.NotFound());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public async Task Handle_WhenChangePasswordFails_ReturnsInvalidResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_claimsPrincipal, "currentPass", "newPass");
        var user = new User(1, "test@example.com", "test@example.com", true);

        _userService.GetUserAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));
        _userService.ChangePasswordAsync(
                Arg.Any<User>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<string>.Error("Failed to change password"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsSuccessResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_claimsPrincipal, "currentPass", "newPass");
        var user = new User(1, "test@example.com", "test@example.com", true);

        _userService.GetUserAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));
        _userService.ChangePasswordAsync(
                Arg.Any<User>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success("Password changed successfully"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Password changed successfully");
    }

    [Fact]
    public async Task Handle_PassesCorrectParametersToChangePassword()
    {
        // Arrange
        const string currentPassword = "currentPass";
        const string newPassword = "newPass";
        var command = new ChangePasswordCommand(_claimsPrincipal, currentPassword, newPassword);
        var user = new User(1, "test@example.com", "test@example.com", true);

        _userService.GetUserAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));
        _userService.ChangePasswordAsync(
                Arg.Any<User>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success("Success"));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _userService.Received(1).ChangePasswordAsync(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<string>(p => p == currentPassword),
            Arg.Is<string>(p => p == newPassword),
            Arg.Any<CancellationToken>()
        );
    }
}
