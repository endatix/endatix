using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Account;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Account.ResetPassword;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Core.Tests.UseCases.Account.ResetPassword;

public class ResetPasswordHandlerTests
{
    private readonly IUserPasswordManageService _userPasswordManageService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ResetPasswordHandler> _logger;
    private readonly ResetPasswordHandler _handler;

    public ResetPasswordHandlerTests()
    {
        _userPasswordManageService = Substitute.For<IUserPasswordManageService>();
        _emailTemplateService = Substitute.For<IEmailTemplateService>();
        _emailSender = Substitute.For<IEmailSender>();
        _logger = Substitute.For<ILogger<ResetPasswordHandler>>();
        _handler = new ResetPasswordHandler(_userPasswordManageService, _emailTemplateService, _emailSender, _logger);
    }

    [Fact]
    public async Task Handle_ValidResetRequest_ResetsPasswordAndSendsEmail_ReturnsSuccessMessage()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        var request = new ResetPasswordCommand(email, resetCode, newPassword);
        var emailMessage = CreatePasswordChangedEmail(email);

        _userPasswordManageService.ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>())
            .Returns(Result.Success("Password reset successfully"));
        _emailTemplateService.CreatePasswordChangedEmail(email)
            .Returns(emailMessage);
        _emailSender.SendEmailAsync(emailMessage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Password changed successfully");

        await _userPasswordManageService.Received(1).ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>());
        _emailTemplateService.Received(1).CreatePasswordChangedEmail(email);
        await _emailSender.Received(1).SendEmailAsync(emailMessage, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidResetRequest_ResetsPasswordButEmailSendingFails_StillReturnsSuccessMessage()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        var request = new ResetPasswordCommand(email, resetCode, newPassword);
        var emailMessage = CreatePasswordChangedEmail(email);
        var emailException = new Exception("Email service unavailable");

        _userPasswordManageService.ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>())
            .Returns(Result.Success("Password reset successfully"));
        _emailTemplateService.CreatePasswordChangedEmail(email)
            .Returns(emailMessage);
        _emailSender.SendEmailAsync(emailMessage, Arg.Any<CancellationToken>())
            .ThrowsAsync(emailException);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Password changed successfully");

        // Password reset succeeded, but email failed - should still return success
        await _userPasswordManageService.Received(1).ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>());
        _emailTemplateService.Received(1).CreatePasswordChangedEmail(email);
        await _emailSender.Received(1).SendEmailAsync(emailMessage, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidResetRequest_ResetsPasswordButEmailTemplateServiceFails_StillReturnsSuccessMessage()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        var request = new ResetPasswordCommand(email, resetCode, newPassword);
        var templateException = new Exception("Template service unavailable");

        _userPasswordManageService.ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>())
            .Returns(Result.Success("Password reset successfully"));
        _emailTemplateService.CreatePasswordChangedEmail(email)
            .Throws(templateException);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Password changed successfully");

        // Password reset succeeded, but template creation failed - should still return success
        await _userPasswordManageService.Received(1).ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>());
        _emailTemplateService.Received(1).CreatePasswordChangedEmail(email);
        await _emailSender.DidNotReceive().SendEmailAsync(Arg.Any<EmailWithTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidResultFromResetPassword_ReturnsInvalidResult()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "invalid-reset-code";
        var newPassword = "NewPassword123!";
        var request = new ResetPasswordCommand(email, resetCode, newPassword);
        var failureResult = Result.Invalid(new ValidationError("Invalid or expired reset code."));

        _userPasswordManageService.ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>())
            .Returns(failureResult);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage == "Invalid or expired reset code.");

        await _userPasswordManageService.Received(1).ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>());
        _emailTemplateService.DidNotReceive().CreatePasswordChangedEmail(Arg.Any<string>());
        await _emailSender.DidNotReceive().SendEmailAsync(Arg.Any<EmailWithTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ErrorResultFromResetPassword_ReturnsErrorResult()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "weak";
        var request = new ResetPasswordCommand(email, resetCode, newPassword);
        var failureResult = Result.Error("Could not reset password. Please try again or contact support.");

        _userPasswordManageService.ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>())
            .Returns(failureResult);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain("Could not reset password. Please try again or contact support.");

        await _userPasswordManageService.Received(1).ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>());
        _emailTemplateService.DidNotReceive().CreatePasswordChangedEmail(Arg.Any<string>());
        await _emailSender.DidNotReceive().SendEmailAsync(Arg.Any<EmailWithTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SystemError_ReturnsFailureResult()
    {
        // Arrange
        var email = "user@example.com";
        var resetCode = "valid-reset-code";
        var newPassword = "NewPassword123!";
        var request = new ResetPasswordCommand(email, resetCode, newPassword);
        var failureResult = Result.Error("An unexpected error occurred.");

        _userPasswordManageService.ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>())
            .Returns(failureResult);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain("An unexpected error occurred.");

        await _userPasswordManageService.Received(1).ResetPasswordAsync(email, resetCode, newPassword, Arg.Any<CancellationToken>());
        _emailTemplateService.DidNotReceive().CreatePasswordChangedEmail(Arg.Any<string>());
        await _emailSender.DidNotReceive().SendEmailAsync(Arg.Any<EmailWithTemplate>(), Arg.Any<CancellationToken>());
    }

    private static EmailWithTemplate CreatePasswordChangedEmail(string email)
    {
        return new EmailWithTemplate
        {
            To = email,
            Subject = "subject",
            TemplateId = "template-id"
        };
    }
}