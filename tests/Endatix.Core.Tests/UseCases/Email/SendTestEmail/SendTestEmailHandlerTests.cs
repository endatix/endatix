using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Email.SendTestEmail;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Tests.UseCases.Email.SendTestEmail;

public class SendTestEmailHandlerTests
{
    private readonly IEmailSender _emailSender;
    private readonly SendTestEmailHandler _sut;

    public SendTestEmailHandlerTests()
    {
        _emailSender = Substitute.For<IEmailSender>();
        _sut = new SendTestEmailHandler(
            _emailSender,
            Substitute.For<ILogger<SendTestEmailHandler>>());
    }

    [Fact]
    public async Task Handle_UnconfiguredSenderForBodyEmail_ReturnsUnavailableWithoutSending()
    {
        _emailSender.IsConfigured.Returns(false);
        var command = new SendTestEmailCommand("admin@example.com");

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Unavailable);
        result.Errors.Should().Contain("Email provider is not configured.");
        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<EmailWithBody>(),
            Arg.Any<CancellationToken>());
        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<EmailWithTemplate>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnconfiguredSenderForTemplateEmail_ReturnsUnavailableWithoutSending()
    {
        _emailSender.IsConfigured.Returns(false);
        var command = new SendTestEmailCommand("admin@example.com", "welcome-email");

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Unavailable);
        result.Errors.Should().Contain("Email provider is not configured.");
        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<EmailWithBody>(),
            Arg.Any<CancellationToken>());
        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<EmailWithTemplate>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhitespaceTemplateId_SendsBodyEmail()
    {
        _emailSender.IsConfigured.Returns(true);
        var command = new SendTestEmailCommand("admin@example.com", " ");

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _emailSender.Received(1).SendEmailAsync(
            Arg.Any<EmailWithBody>(),
            Arg.Any<CancellationToken>());
        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<EmailWithTemplate>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BodyEmailSendFails_ReturnsSafeError()
    {
        _emailSender.IsConfigured.Returns(true);
        _emailSender
            .SendEmailAsync(Arg.Any<EmailWithBody>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("SMTP auth failed"));
        var command = new SendTestEmailCommand("admin@example.com");

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain("Failed to send test email.");
        result.Errors.Should().NotContain(error => error.Contains("SMTP auth failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_TemplateEmailSendFails_ReturnsSafeError()
    {
        _emailSender.IsConfigured.Returns(true);
        _emailSender
            .SendEmailAsync(Arg.Any<EmailWithTemplate>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("HTTP provider failed"));
        var command = new SendTestEmailCommand("admin@example.com", "welcome-email");

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain("Failed to send test email.");
        result.Errors.Should().NotContain(error => error.Contains("HTTP provider failed", StringComparison.Ordinal));
    }
}
