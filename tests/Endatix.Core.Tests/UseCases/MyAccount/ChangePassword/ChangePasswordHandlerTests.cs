using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.MyAccount.ChangePassword;

namespace Endatix.Core.Tests.UseCases.MyAccount.ChangePassword;

public class ChangePasswordHandlerTests
{
    private readonly IUserService _userService;
    private readonly ChangePasswordHandler _handler;
    private readonly long _testUserId = 123L;

    public ChangePasswordHandlerTests()
    {
        _userService = Substitute.For<IUserService>();
        _handler = new ChangePasswordHandler(_userService);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsNull_ReturnsInvalidResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(null, "currentPass", "newPass");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage == "User not found");
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsInvalidResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_testUserId, "currentPass", "newPass");
        _userService.GetUserAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Result<User>.NotFound());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage == "User not found");
    }

    [Fact]
    public async Task Handle_WhenChangePasswordFails_ReturnsInvalidResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_testUserId, "currentPass", "newPass");
        var user = new User(_testUserId, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);

        _userService.GetUserAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
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
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage == "Failed to change password");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsSuccessResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_testUserId, "currentPass", "newPass");
        var user = new User(_testUserId, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);

        _userService.GetUserAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
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
        var command = new ChangePasswordCommand(_testUserId, currentPassword, newPassword);
        var user = new User(_testUserId, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);

        _userService.GetUserAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
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
        await _userService.Received(1).GetUserAsync(
            Arg.Is<long>(id => id == _testUserId),
            Arg.Any<CancellationToken>()
        );
        
        await _userService.Received(1).ChangePasswordAsync(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<string>(p => p == currentPassword),
            Arg.Is<string>(p => p == newPassword),
            Arg.Any<CancellationToken>()
        );
    }
}
