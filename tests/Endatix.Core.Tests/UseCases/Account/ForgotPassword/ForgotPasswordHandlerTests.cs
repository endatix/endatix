using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Account;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Account.ForgotPassword;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Core.Tests.UseCases.Account.ForgotPassword;

public class ForgotPasswordHandlerTests
{
    private readonly IUserPasswordManageService _userPasswordManageService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ForgotPasswordHandler> _logger;
    private readonly ForgotPasswordHandler _handler;

    public ForgotPasswordHandlerTests()
    {
        _userPasswordManageService = Substitute.For<IUserPasswordManageService>();
        _emailTemplateService = Substitute.For<IEmailTemplateService>();
        _emailSender = Substitute.For<IEmailSender>();
        _logger = Substitute.For<ILogger<ForgotPasswordHandler>>();
        _handler = new ForgotPasswordHandler(_userPasswordManageService, _emailTemplateService, _emailSender, _logger);
    }

    [Fact]
    public async Task Handle_ValidEmail_GeneratesTokenAndSendsEmail_ReturnsSuccessMessage()
    {
        // Arrange
        var email = "user@example.com";
        var resetToken = "valid-reset-token";
        var request = new ForgotPasswordCommand(email);
        var emailMessage = CreateForgotPasswordEmail(email, resetToken);

        _userPasswordManageService.GeneratePasswordResetTokenAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result.Success(resetToken));
        _emailTemplateService.CreateForgotPasswordEmail(email, resetToken)
            .Returns(emailMessage);
        _emailSender.SendEmailAsync(emailMessage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ForgotPasswordHandler.GENERAL_SUCCESS_MESSAGE);

        await _userPasswordManageService.Received(1).GeneratePasswordResetTokenAsync(email, Arg.Any<CancellationToken>());
        _emailTemplateService.Received(1).CreateForgotPasswordEmail(email, resetToken);
        await _emailSender.Received(1).SendEmailAsync(emailMessage, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEmail_GeneratesTokenButEmailSendingFails_ReturnsErrorMessage()
    {
        // Arrange
        var email = "user@example.com";
        var resetToken = "valid-reset-token";
        var request = new ForgotPasswordCommand(email);
        var emailMessage = CreateForgotPasswordEmail(email, resetToken);
        var emailException = new Exception("Email service unavailable");

        _userPasswordManageService.GeneratePasswordResetTokenAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result.Success(resetToken));
        _emailTemplateService.CreateForgotPasswordEmail(email, resetToken)
            .Returns(emailMessage);
        _emailSender.SendEmailAsync(emailMessage, Arg.Any<CancellationToken>())
            .ThrowsAsync(emailException);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(ForgotPasswordHandler.FAILED_TO_SEND_EMAIL_MESSAGE);

        await _userPasswordManageService.Received(1).GeneratePasswordResetTokenAsync(email, Arg.Any<CancellationToken>());
        _emailTemplateService.Received(1).CreateForgotPasswordEmail(email, resetToken);
        await _emailSender.Received(1).SendEmailAsync(emailMessage, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEmail_GeneratesTokenButEmailTemplateServiceFails_ReturnsErrorMessage()
    {
        // Arrange
        var email = "user@example.com";
        var resetToken = "valid-reset-token";
        var request = new ForgotPasswordCommand(email);
        var templateException = new Exception("Template service unavailable");

        _userPasswordManageService.GeneratePasswordResetTokenAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result.Success(resetToken));
        _emailTemplateService.CreateForgotPasswordEmail(email, resetToken)
            .Throws(templateException);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(ForgotPasswordHandler.FAILED_TO_SEND_EMAIL_MESSAGE);

        await _userPasswordManageService.Received(1).GeneratePasswordResetTokenAsync(email, Arg.Any<CancellationToken>());
        _emailTemplateService.Received(1).CreateForgotPasswordEmail(email, resetToken);
        await _emailSender.DidNotReceive().SendEmailAsync(Arg.Any<EmailWithTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEmail_GeneratesTokenButEmailSenderThrowsSpecificException_ReturnsErrorMessage()
    {
        // Arrange
        var email = "user@example.com";
        var resetToken = "valid-reset-token";
        var request = new ForgotPasswordCommand(email);
        var emailMessage = CreateForgotPasswordEmail(email, resetToken);
        var emailException = new InvalidOperationException("SMTP connection failed");

        _userPasswordManageService.GeneratePasswordResetTokenAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result.Success(resetToken));
        _emailTemplateService.CreateForgotPasswordEmail(email, resetToken)
            .Returns(emailMessage);
        _emailSender.SendEmailAsync(emailMessage, Arg.Any<CancellationToken>())
            .ThrowsAsync(emailException);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(ForgotPasswordHandler.FAILED_TO_SEND_EMAIL_MESSAGE);
    }

    [Fact]
    public async Task Handle_ValidEmail_GeneratesTokenSuccessfully()
    {
        // Arrange
        var email = "user@example.com";
        var resetToken = "valid-reset-token";
        var request = new ForgotPasswordCommand(email);
        var emailMessage = CreateForgotPasswordEmail(email, resetToken);

        _userPasswordManageService.GeneratePasswordResetTokenAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result.Success(resetToken));
        _emailTemplateService.CreateForgotPasswordEmail(email, resetToken)
            .Returns(emailMessage);
        _emailSender.SendEmailAsync(emailMessage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ForgotPasswordHandler.GENERAL_SUCCESS_MESSAGE);
    }

    [Fact]
    public async Task Handle_ValidEmail_GeneratesTokenSuccessfully_ReturnsGeneralSuccessMessageForSecurity()
    {
        // Arrange
        var email = "user@example.com";
        var resetToken = "valid-reset-token";
        var request = new ForgotPasswordCommand(email);
        var emailMessage = CreateForgotPasswordEmail(email, resetToken);

        _userPasswordManageService.GeneratePasswordResetTokenAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result.Success(resetToken));
        _emailTemplateService.CreateForgotPasswordEmail(email, resetToken)
            .Returns(emailMessage);
        _emailSender.SendEmailAsync(emailMessage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ForgotPasswordHandler.GENERAL_SUCCESS_MESSAGE);

        // Verify the security measure: always return the same message regardless of whether email was sent
        result.Value.Should().Be("Thank you. If an account exists with this email, you will receive an email with instructions to reset your password.");
    }

    private static EmailWithTemplate CreateForgotPasswordEmail(string email, string resetToken)
    {
        return new EmailWithTemplate
        {
            To = email,
            Subject = "subject",
            TemplateId = "template-id",
            Metadata = new Dictionary<string, object>
            {
                ["hubUrl"] = "https://hub.endatix.com",
                ["resetCodeQuery"] = $"email={email}&resetCode={resetToken}"
            }
        };
    }
}