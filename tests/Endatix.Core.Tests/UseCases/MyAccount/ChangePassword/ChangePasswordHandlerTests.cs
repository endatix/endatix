using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Account;
using Endatix.Core.Entities;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.MyAccount.ChangePassword;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Core.Tests.UseCases.MyAccount.ChangePassword;

public class ChangePasswordHandlerTests
{
    private readonly IUserPasswordManageService _passwordService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ChangePasswordHandler> _logger;
    private readonly ChangePasswordHandler _handler;
    private readonly long _testUserId = 123L;

    public ChangePasswordHandlerTests()
    {
        _passwordService = Substitute.For<IUserPasswordManageService>();
        _emailTemplateService = Substitute.For<IEmailTemplateService>();
        _emailSender = Substitute.For<IEmailSender>();
        _logger = Substitute.For<ILogger<ChangePasswordHandler>>();
        _handler = new ChangePasswordHandler(_passwordService, _emailTemplateService, _emailSender, _logger);
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

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task Handle_WhenUserIdIsNegativeOrZero_ReturnsInvalidResult(int userId)
    {
        // Arrange
        var command = new ChangePasswordCommand(userId, "currentPass", "newPass");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage == "User not found");
    }

    [Fact]
    public async Task Handle_WhenChangePasswordFailsWithValidationError_ReturnsInvalidResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_testUserId, "currentPass", "newPass");
        _passwordService.ChangePasswordAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<User>.Invalid(new ValidationError("User not found")));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().HaveCount(1);
        result.ValidationErrors.First().ErrorMessage.Should().Be("User not found");
    }

    [Fact]
    public async Task Handle_WhenChangePasswordFailsWithError_ReturnsErrorResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_testUserId, "currentPass", "newPass");
        var user = new User(_testUserId, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);

        _passwordService.ChangePasswordAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Error("Failed to change password"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().HaveCount(1);
        result.Errors.First().Should().Be("Failed to change password");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsSuccessResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_testUserId, "currentPass", "newPass");
        var user = new User(_testUserId, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);

        _passwordService.ChangePasswordAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Password changed successfully");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_SendsEmail()
    {
        // Arrange
        var command = new ChangePasswordCommand(_testUserId, "currentPass", "newPass");
        var user = new User(_testUserId, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);

        _passwordService.ChangePasswordAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _emailSender.Received(1).SendEmailAsync(Arg.Any<EmailWithTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ButEmailSendingFails_ReturnsSuccessResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_testUserId, "currentPass", "newPass");
        var user = new User(_testUserId, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);

        _passwordService.ChangePasswordAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        _emailSender.SendEmailAsync(Arg.Any<EmailWithTemplate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Failed to send email"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _emailSender.Received(1).SendEmailAsync(Arg.Any<EmailWithTemplate>(), Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task Handle_WhenChangePassword_FailsWithNotExpectedResult_ReturnsErrorResult()
    {
        // Arrange
        var command = new ChangePasswordCommand(_testUserId, "currentPass", "newPass");
        var user = new User(_testUserId, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);
        var notExpectedResult = Result.NotFound();

        _passwordService.ChangePasswordAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(notExpectedResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().HaveCount(1);
        result.Errors.First().Should().Be("An unexpected error occurred");
    }

    [Fact]
    public async Task Handle_PassesCorrectParametersToChangePassword()
    {
        // Arrange
        const string currentPassword = "currentPass";
        const string newPassword = "newPass";
        var command = new ChangePasswordCommand(_testUserId, currentPassword, newPassword);
        var user = new User(_testUserId, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);

        _passwordService.ChangePasswordAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _passwordService.Received(1).ChangePasswordAsync(
            Arg.Is<long>(id => id == _testUserId),
            Arg.Is<string>(p => p == currentPassword),
            Arg.Is<string>(p => p == newPassword),
            Arg.Any<CancellationToken>()
        );
    }
}
