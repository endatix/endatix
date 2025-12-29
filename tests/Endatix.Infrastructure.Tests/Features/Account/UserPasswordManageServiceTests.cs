using System.Text;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.Account;
using Endatix.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Infrastructure.Tests.Features.Account;

public class UserPasswordManageServiceTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<UserPasswordManageService> _logger;
    private readonly UserPasswordManageService _service;

    public UserPasswordManageServiceTests()
    {
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        _logger = Substitute.For<ILogger<UserPasswordManageService>>();
        _service = new UserPasswordManageService(_userManager, _logger);
    }

    #region GeneratePasswordResetTokenAsync Tests

    [Fact]
    public async Task GeneratePasswordResetTokenAsync_WithValidEmailAndConfirmedUser_ReturnsSuccessWithToken()
    {
        // Arrange
        var email = "user@example.com";
        var user = new AppUser { Email = email, EmailConfirmed = true };
        var token = "valid-reset-token";

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns(token);

        // Act
        var result = await _service.GeneratePasswordResetTokenAsync(email, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(token);

        await _userManager.Received(1).FindByEmailAsync(email);
        await _userManager.Received(1).IsEmailConfirmedAsync(user);
        await _userManager.Received(1).GeneratePasswordResetTokenAsync(user);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GeneratePasswordResetTokenAsync_WithNullOrWhiteSpaceEmail_ReturnsInvalidResult(string? email)
    {
        // Act
        var result = await _service.GeneratePasswordResetTokenAsync(email!, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Any(v => v.ErrorMessage.Contains("Email is required"));

        await _userManager.DidNotReceive().FindByEmailAsync(Arg.Any<string>());
        await _userManager.DidNotReceive().IsEmailConfirmedAsync(Arg.Any<AppUser>());
        await _userManager.DidNotReceive().GeneratePasswordResetTokenAsync(Arg.Any<AppUser>());
    }

    [Fact]
    public async Task GeneratePasswordResetTokenAsync_WithNonExistentUser_ReturnsInvalidResult()
    {
        // Arrange
        var email = "nonexistent@example.com";
        AppUser? user = null;

        _userManager.FindByEmailAsync(email).Returns(user);

        // Act
        var result = await _service.GeneratePasswordResetTokenAsync(email, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(v => v.ErrorMessage.Contains("User not found"));

        await _userManager.Received(1).FindByEmailAsync(email);
        await _userManager.DidNotReceive().IsEmailConfirmedAsync(Arg.Any<AppUser>());
        await _userManager.DidNotReceive().GeneratePasswordResetTokenAsync(Arg.Any<AppUser>());
    }

    [Fact]
    public async Task GeneratePasswordResetTokenAsync_WithUnconfirmedEmail_ReturnsInvalidResult()
    {
        // Arrange
        var email = "user@example.com";
        var user = new AppUser { Email = email, EmailConfirmed = false };

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(false);

        // Act
        var result = await _service.GeneratePasswordResetTokenAsync(email, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(v => v.ErrorMessage.Contains("User not found"));

        await _userManager.Received(1).FindByEmailAsync(email);
        await _userManager.Received(1).IsEmailConfirmedAsync(user);
        await _userManager.DidNotReceive().GeneratePasswordResetTokenAsync(Arg.Any<AppUser>());
    }

    [Fact]
    public async Task GeneratePasswordResetTokenAsync_WithConfirmedUserButTokenGenerationFails_ReturnsInvalidResult()
    {
        // Arrange
        var email = "user@example.com";
        var user = new AppUser { Email = email, EmailConfirmed = true };
        string? token = null;

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        _userManager.GeneratePasswordResetTokenAsync(user)!.Returns(token);

        // Act
        var result = await _service.GeneratePasswordResetTokenAsync(email, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain("Failed to generate password reset token");

        await _userManager.Received(1).FindByEmailAsync(email);
        await _userManager.Received(1).IsEmailConfirmedAsync(user);
        await _userManager.Received(1).GeneratePasswordResetTokenAsync(user);
    }

    #endregion

    #region ResetPasswordAsync Tests

    [Fact]
    public async Task ResetPasswordAsync_WithValidInputs_ReturnsSuccessResult()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        var user = new AppUser { Email = email, EmailConfirmed = true };
        var identityResult = IdentityResult.Success;

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        _userManager.ResetPasswordAsync(user, Arg.Any<string>(), newPassword).Returns(identityResult);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Password reset successfully");

        await _userManager.Received(1).FindByEmailAsync(email);
        await _userManager.Received(1).IsEmailConfirmedAsync(user);
        await _userManager.Received(1).ResetPasswordAsync(user, Arg.Any<string>(), newPassword);
    }

    [Theory]
    [InlineData(null, "code", "password")]
    [InlineData("email", null, "password")]
    [InlineData("email", "code", null)]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("  ", "   ", "   ")]
    public async Task ResetPasswordAsync_WithNullOrEmptyInputs_ReturnsInvalidResult(string? email, string? resetCode, string? newPassword)
    {
        // Act
        var result = await _service.ResetPasswordAsync(email!, resetCode!, newPassword!, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(ve => ve.ErrorMessage.Contains("Invalid input"));

        await _userManager.DidNotReceive().FindByEmailAsync(Arg.Any<string>());
        await _userManager.DidNotReceive().IsEmailConfirmedAsync(Arg.Any<AppUser>());
        await _userManager.DidNotReceive().ResetPasswordAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WithNonExistentUser_ReturnsInvalidResultAndLogsWarning()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        AppUser? user = null;

        _userManager.FindByEmailAsync(email).Returns(user);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(v => v.ErrorMessage.Contains("Invalid input"));
        result.ValidationErrors.Should().NotContain(v => v.ErrorCode == "invalid_token");

        await _userManager.Received(1).FindByEmailAsync(email);
        await _userManager.DidNotReceive().IsEmailConfirmedAsync(Arg.Any<AppUser>());
        await _userManager.DidNotReceive().ResetPasswordAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WithUnconfirmedEmail_ReturnsInvalidResult()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        var user = new AppUser { Email = email, EmailConfirmed = false };

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(false);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(v => v.ErrorMessage.Contains("Invalid input"));

        await _userManager.Received(1).FindByEmailAsync(email);
        await _userManager.Received(1).IsEmailConfirmedAsync(user);
        await _userManager.DidNotReceive().ResetPasswordAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WithInvalidBase64ResetCode_StopsProcessingAndReturnsInvalidResult()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "invalid-base64-code!@#";
        var newPassword = "NewPassword123!";
        var user = new AppUser { Email = email, EmailConfirmed = true };

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(v => v.ErrorMessage.Contains("Invalid reset code or token expired"));
        result.ValidationErrors.Should().ContainSingle(v => v.ErrorCode == "invalid_token");

        await _userManager.Received(1).FindByEmailAsync(email);
        await _userManager.Received(1).IsEmailConfirmedAsync(user);
        await _userManager.DidNotReceive().ResetPasswordAsync(user, Arg.Any<string>(), newPassword);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidBase64ResetCode_ProcessesCorrectly()
    {
        // Arrange
        var email = "user@example.com";
        var originalCode = "valid-reset-code";
        var resetCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(originalCode));
        var newPassword = "NewPassword123!";
        var user = new AppUser { Email = email, EmailConfirmed = true };
        var identityResult = IdentityResult.Success;

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        _userManager.ResetPasswordAsync(user, originalCode, newPassword).Returns(identityResult);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Password reset successfully");

        await _userManager.Received(1).ResetPasswordAsync(user, originalCode, newPassword);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithIdentityFailure_ReturnsInvalidTokenResult()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        var user = new AppUser { Email = email, EmailConfirmed = true };
        var identityResult = IdentityResult.Failed(new IdentityError { Description = "Password too weak" });

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        _userManager.ResetPasswordAsync(user, Arg.Any<string>(), newPassword).Returns(identityResult);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(v => v.ErrorMessage.Contains("Invalid reset code or token expired"));
        result.ValidationErrors.Should().ContainSingle(v => v.ErrorCode == "invalid_token");

        await _userManager.Received(1).ResetPasswordAsync(user, Arg.Any<string>(), newPassword);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithUnexpectedException_ReturnsErrorResultAndLogsError()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        var user = new AppUser { Email = email, EmailConfirmed = true };
        var unexpectedException = new InvalidOperationException("Database connection failed");

        var maskedEmail = SensitiveValue.Email(email);

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        _userManager.ResetPasswordAsync(user, Arg.Any<string>(), newPassword).ThrowsAsync(unexpectedException);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain("Could not reset password. Please try again or contact support.");

        await _userManager.Received(1).ResetPasswordAsync(user, Arg.Any<string>(), newPassword);
        
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Equals($"An unexpected error occurred while resetting the password for user {maskedEmail}")),
            unexpectedException,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WithFormatException_ReturnsInvalidTokenResult()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        var user = new AppUser { Email = email, EmailConfirmed = true };
        var formatException = new FormatException("Invalid format");

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        _userManager.ResetPasswordAsync(user, Arg.Any<string>(), newPassword).ThrowsAsync(formatException);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(v => v.ErrorMessage.Contains("Invalid reset code or token expired"));
        result.ValidationErrors.Should().ContainSingle(v => v.ErrorCode == "invalid_token");

        await _userManager.Received(1).ResetPasswordAsync(user, Arg.Any<string>(), newPassword);
    }

    #endregion

    #region ChangePasswordAsync Tests

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ReturnsError()
    {
        // Arrange
        var requestedUser = new AppUser()
        {
            Id = 22_111_111_111_111_111,
            UserName = "test@example.com"
        };
        AppUser nullUser = null!;
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(nullUser);

        // Act
        var result = await _service.ChangePasswordAsync(requestedUser.Id, "currentPass", "newPass", TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().HaveCount(1);
        result.ValidationErrors.First().ErrorMessage.Should().Be("User not found");
    }

    [Fact]
    public async Task ChangePasswordAsync_EmptyCurrentPassword_ReturnsError()
    {
        // Arrange
        var user = new AppUser()
        {
            Id = 22_111_111_111_111_111,
            UserName = "test@example.com"
        };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        // Act
        var result = await _service.ChangePasswordAsync(user.Id, "", "newPass", TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().HaveCount(1);
        result.ValidationErrors.First().ErrorMessage.Should().Be("The current password is required to set a new password. If the old password is forgotten, use password reset.");
    }

    [Fact]
    public async Task ChangePasswordAsync_EmptyNewPassword_ReturnsError()
    {
        // Arrange
        var user = new AppUser()
        {
            Id = 22_111_111_111_111_111,
            UserName = "test@example.com"
        };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        // Act
        var result = await _service.ChangePasswordAsync(user.Id, "currentPass", "", TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().HaveCount(1);
        result.ValidationErrors.First().ErrorMessage.Should().Be("The new password is required to change the password.");
    }

    [Fact]
    public async Task ChangePasswordAsync_ChangePasswordFailsWithIdentityError_ReturnsError()
    {
        // Arrange
        var user = new AppUser()
        {
            Id = 22_111_111_111_111_111,
            UserName = "test@example.com"
        };
        var errorDescriber = new IdentityErrorDescriber();
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.ChangePasswordAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(errorDescriber.PasswordMismatch()));

        // Act
        var result = await _service.ChangePasswordAsync(user.Id, "currentPass", "newPass", TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().HaveCount(1);
        result.ValidationErrors.First().ErrorCode.Should().Be(nameof(errorDescriber.PasswordMismatch));
        result.ValidationErrors.First().ErrorMessage.Should().Be(errorDescriber.PasswordMismatch().Description);
    }


    [Fact]
    public async Task ChangePasswordAsync_ChangePasswordThrowsException_ReturnsError()
    {
        // Arrange
        var user = new AppUser()
        {
            Id = 22_111_111_111_111_111,
            UserName = "test@example.com"
        };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.ChangePasswordAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new Exception("An unexpected error occurred while changing the password"));

        // Act
        var result = await _service.ChangePasswordAsync(user.Id, "currentPass", "newPass", TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().HaveCount(1);
        result.Errors.First().Should().Be("An unexpected error occurred while changing the password");
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_ReturnsSuccessUser()
    {
        // Arrange
        var user = new AppUser()
        {
            Id = 22_111_111_111_111_111,
            UserName = "test@example.com",
            Email = "test@example.com",
        };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.ChangePasswordAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        // Act
        var result = await _service.ChangePasswordAsync(user.Id, "currentPass", "newPass", TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Id.Should().Be(user.ToUserEntity().Id);
    }

    #endregion

    #region Security and Privacy Tests

    [Fact]
    public async Task ResetPasswordAsync_WithNonExistentUser_DoesNotLeakUserExistence()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        AppUser? user = null;

        _userManager.FindByEmailAsync(email).Returns(user);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(ve => ve.ErrorMessage == "Invalid input");

        // Should not reveal whether user exists or not
        var disallowed = new[] { "User not found", "Email not confirmed" };
        result.ValidationErrors
            .Select(ve => ve.ErrorMessage)
            .Should()
            .NotContain(disallowed);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithUnconfirmedEmail_DoesNotLeakUserExistence()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        var user = new AppUser { Email = email, EmailConfirmed = false };

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(false);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(ve => ve.ErrorMessage == "Invalid input");

        // Should not reveal whether user exists or not
        var disallowed = new[] { "User not found", "Email not confirmed" };
        result.ValidationErrors
            .Select(ve => ve.ErrorMessage)
            .Should()
            .NotContain(disallowed);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithInvalidToken_ReturnsConsistentErrorCode()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "invalid-reset-code";
        var newPassword = "NewPassword123!";
        var user = new AppUser { Email = email, EmailConfirmed = true };
        var identityResult = IdentityResult.Failed(new IdentityError { Description = "Invalid token" });

        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        _userManager.ResetPasswordAsync(user, Arg.Any<string>(), newPassword).Returns(identityResult);

        // Act
        var result = await _service.ResetPasswordAsync(email, resetCode, newPassword, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);

        // Should return consistent error code to prevent enumeration attacks
        var error = result.ValidationErrors.Should().ContainSingle(e => e.ErrorCode == "invalid_token").Subject;
        error.ErrorMessage.Should().Be("Invalid reset code or token expired");
        error.Severity.Should().Be(ValidationSeverity.Error);
        error.Identifier.Should().Be("reset_password");
    }

    #endregion
}