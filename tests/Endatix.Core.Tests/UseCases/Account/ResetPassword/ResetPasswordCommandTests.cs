using Endatix.Core.UseCases.Account.ResetPassword;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Account.ResetPassword;

public class ResetPasswordCommandTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesCommandWithCorrectValues()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";

        // Act
        var command = new ResetPasswordCommand(email, resetCode, newPassword);

        // Assert
        command.Email.Should().Be(email);
        command.ResetCode.Should().Be(resetCode);
        command.NewPassword.Should().Be(newPassword);
    }

    [Fact]
    public void Constructor_WithEmptyEmail_ThrowsArgumentException()
    {
        // Arrange
        var email = string.Empty;
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";

        // Act
        Action act = () => new ResetPasswordCommand(email, resetCode, newPassword);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(email), Empty));
    }

    [Fact]
    public void Constructor_WithEmptyResetCode_ThrowsArgumentException()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = string.Empty;
        var newPassword = "NewPassword123!";

        // Act
        Action act = () => new ResetPasswordCommand(email, resetCode, newPassword);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(resetCode), Empty));
    }

    [Fact]
    public void Constructor_WithEmptyNewPassword_ThrowsArgumentException()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = string.Empty;

        // Act
        Action act = () => new ResetPasswordCommand(email, resetCode, newPassword);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(newPassword), Empty));
    }
}