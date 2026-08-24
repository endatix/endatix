using MailKit;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Endatix.Core;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Email;

namespace Endatix.Infrastructure.Tests.Email;

public class SmtpEmailSenderTests
{
    public SmtpEmailSenderTests()
    {
        // The one-time warning gates are process-wide (static) by design; reset them before every
        // test so execution order can't determine whether a given test observes a warning.
        SmtpEmailSender.ResetWarningGatesForTests();
    }

    [Fact]
    public void ProviderMetadata_DefaultSettings_ReturnsUnconfiguredSmtp()
    {
        var sut = new SmtpEmailSender(
            Substitute.For<ILogger<SmtpEmailSender>>(),
            Options.Create(new SmtpSettings
            {
                Host = "localhost",
                DefaultFromAddress = "noreply@example.com"
            }),
            new EmailTemplateRenderer(
                Substitute.For<IRepository<EmailTemplate>>(),
                Options.Create(new EmailTemplateSettings())));

        sut.ProviderName.Should().Be("SMTP");
        sut.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task SendEmailWithTemplate_ExternalTemplate_StillUsesDatabaseTemplate()
    {
        var templateRepository = Substitute.For<IRepository<EmailTemplate>>();
        var sut = new SmtpEmailSender(
            Substitute.For<ILogger<SmtpEmailSender>>(),
            Options.Create(new SmtpSettings
            {
                Host = "localhost",
                DefaultFromAddress = "noreply@example.com"
            }),
            new EmailTemplateRenderer(
                templateRepository,
                Options.Create(new EmailTemplateSettings())));

        var email = new EmailWithTemplate
        {
            To = "recipient@example.com",
            TemplateId = "external-template",
            IsExternal = true
        };

        Func<Task> act = () => sut.SendEmailAsync(email, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Email template 'external-template' not found in database");

        await templateRepository.Received(1).FirstOrDefaultAsync(
            Arg.Any<EmailTemplateByNameSpec>(),
            Arg.Any<CancellationToken>());
    }

    // -- ResolveSecureSocketOptions mapping --

    [Theory]
    [InlineData(587, true, SmtpSecurityMode.Auto, SecureSocketOptions.StartTls)]
    [InlineData(465, true, SmtpSecurityMode.Auto, SecureSocketOptions.SslOnConnect)]
    [InlineData(25, true, SmtpSecurityMode.Auto, SecureSocketOptions.StartTls)]
    [InlineData(587, false, SmtpSecurityMode.Auto, SecureSocketOptions.None)]
    [InlineData(465, false, SmtpSecurityMode.Auto, SecureSocketOptions.None)]
    [InlineData(25, false, SmtpSecurityMode.Auto, SecureSocketOptions.None)]
    [InlineData(587, true, SmtpSecurityMode.None, SecureSocketOptions.None)]
    [InlineData(587, true, SmtpSecurityMode.StartTls, SecureSocketOptions.StartTls)]
    [InlineData(587, true, SmtpSecurityMode.StartTlsWhenAvailable, SecureSocketOptions.StartTlsWhenAvailable)]
    [InlineData(587, true, SmtpSecurityMode.SslOnConnect, SecureSocketOptions.SslOnConnect)]
    [InlineData(465, false, SmtpSecurityMode.SslOnConnect, SecureSocketOptions.SslOnConnect)]
    public void ResolveSecureSocketOptions_PortEnableSslAndMode_ReturnsExpectedOption(
        int port, bool enableSsl, SmtpSecurityMode mode, SecureSocketOptions expected)
    {
        // Arrange
        var sut = new SmtpEmailSender(
            Substitute.For<ILogger<SmtpEmailSender>>(),
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                Port = port,
                EnableSsl = enableSsl,
                SecurityMode = mode,
                DefaultFromAddress = "noreply@example.com"
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()));

        // Act
        var result = sut.ResolveSecureSocketOptions();

        // Assert
        result.Should().Be(expected);
    }

    // -- SendEmailAsync(EmailWithBody) transport tests --

    [Fact]
    public async Task SendEmailWithBody_ValidEmail_ConnectsAuthenticatesAndSends()
    {
        // Arrange
        var transport = Substitute.For<IMailTransport>();
        var sut = new TestableSmtpEmailSender(
            Substitute.For<ILogger<SmtpEmailSender>>(),
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                Port = 587,
                Username = "user",
                Password = "pass",
                DefaultFromAddress = "noreply@example.com",
                DefaultFromName = "Endatix"
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()),
            transport);

        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        await sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        await transport.Received(1).ConnectAsync("smtp.example.com", 587, SecureSocketOptions.StartTls, Arg.Any<CancellationToken>());
        await transport.Received(1).AuthenticateAsync("user", "pass", Arg.Any<CancellationToken>());
        await transport.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailWithBody_BlankUsername_SkipsAuthentication()
    {
        // Arrange
        var transport = Substitute.For<IMailTransport>();
        var sut = new TestableSmtpEmailSender(
            Substitute.For<ILogger<SmtpEmailSender>>(),
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                DefaultFromAddress = "noreply@example.com"
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()),
            transport);

        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        await sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        await transport.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailWithBody_BlankUsername_LogsWarningOnce()
    {
        // Arrange
        // ILogger.LogWarning is an extension method, not an interface member, so NSubstitute can't
        // verify it directly — inspect the recorded calls to the real Log<TState> method instead.
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var transport = Substitute.For<IMailTransport>();
        var sut = new TestableSmtpEmailSender(
            logger,
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                DefaultFromAddress = "noreply@example.com"
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()),
            transport);

        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        // Sent twice: with only one send, this assertion would pass identically whether the gate
        // works or was deleted entirely — a second send is what actually exercises "once".
        await sut.SendEmailAsync(email, CancellationToken.None);
        await sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        logger.ReceivedCalls().Should().ContainSingle(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) &&
            (LogLevel)call.GetArguments()[0]! == LogLevel.Warning);
    }

    [Fact]
    public async Task SendEmailWithBody_CheckCertificateRevocationConfigured_AppliesToTransportBeforeConnect()
    {
        // Arrange
        // CheckCertificateRevocation defaults to false on both SmtpSettings and the NSubstitute fake,
        // so this must configure `true` — otherwise the assertion would pass even if production code
        // never wired the setting through at all.
        var transport = Substitute.For<IMailTransport>();
        var sut = new TestableSmtpEmailSender(
            Substitute.For<ILogger<SmtpEmailSender>>(),
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                DefaultFromAddress = "noreply@example.com",
                CheckCertificateRevocation = true
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()),
            transport);

        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        await sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        transport.CheckCertificateRevocation.Should().BeTrue();
    }

    [Fact]
    public async Task SendEmailWithBody_HtmlAndPlainText_BuildsMultipartAlternativeWithPlainTextFirst()
    {
        // Arrange
        // SendEmailAsync disposes the MimeMessage (and its Body tree) in a `using` before returning,
        // so the structure must be inspected inside the Arg.Do callback, not from a captured reference
        // afterward — the latter throws ObjectDisposedException.
        var transport = Substitute.For<IMailTransport>();
        bool? isMultipartAlternative = null;
        int? partCount = null;
        string? firstPartMimeType = null;
        string? secondPartMimeType = null;

        transport.SendAsync(Arg.Do<MimeMessage>(msg =>
        {
            isMultipartAlternative = msg.Body is MultipartAlternative;
            if (msg.Body is Multipart multipart)
            {
                partCount = multipart.Count;
                firstPartMimeType = (multipart[0] as TextPart)?.ContentType.MimeType;
                secondPartMimeType = (multipart[1] as TextPart)?.ContentType.MimeType;
            }
        }), Arg.Any<CancellationToken>()).Returns("250 OK");

        var sut = new TestableSmtpEmailSender(
            Substitute.For<ILogger<SmtpEmailSender>>(),
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                DefaultFromAddress = "noreply@example.com",
                DefaultFromName = "Endatix"
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()),
            transport);

        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        await sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        isMultipartAlternative.Should().BeTrue();
        partCount.Should().Be(2);
        firstPartMimeType.Should().Be("text/plain");
        secondPartMimeType.Should().Be("text/html");
    }

    [Fact]
    public async Task SendEmailWithBody_SendThrows_StillDisconnects()
    {
        // Arrange
        var transport = Substitute.For<IMailTransport>();
        transport.IsConnected.Returns(true);
        transport.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("boom")));

        var sut = new TestableSmtpEmailSender(
            Substitute.For<ILogger<SmtpEmailSender>>(),
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                DefaultFromAddress = "noreply@example.com"
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()),
            transport);

        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        Func<Task> act = () => sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        // Pinned to CancellationToken.None (not Arg.Any) — this is the exact regression the
        // production code's finally-block comment exists to prevent: passing the ambient,
        // possibly-cancelled token here would mask the original SendAsync exception.
        await act.Should().ThrowAsync<InvalidOperationException>();
        await transport.Received(1).DisconnectAsync(true, CancellationToken.None);
    }

    [Fact]
    public async Task SendEmailWithBody_SendSucceedsButDisconnectThrows_DoesNotPropagateAndLogsDisconnectFailure()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var transport = Substitute.For<IMailTransport>();
        transport.IsConnected.Returns(true);
        transport.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>()).Returns("250 OK");
        transport.DisconnectAsync(true, CancellationToken.None)
            .Returns(Task.FromException(new IOException("disconnect failed")));

        var sut = new TestableSmtpEmailSender(
            logger,
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                Username = "user",
                Password = "pass",
                DefaultFromAddress = "noreply@example.com"
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()),
            transport);

        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        Func<Task> act = () => sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        // The relay already accepted the message — a disconnect failure during cleanup must not be
        // reported to the caller as the send itself having failed, or a retry would resend mail that
        // already went out.
        await act.Should().NotThrowAsync();

        logger.ReceivedCalls().Should().ContainSingle(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) &&
            (LogLevel)call.GetArguments()[0]! == LogLevel.Warning);
    }

    [Fact]
    public async Task SendEmailWithBody_SendAndDisconnectBothThrow_PropagatesSendFailureAndLogsDisconnectFailure()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var transport = Substitute.For<IMailTransport>();
        transport.IsConnected.Returns(true);
        var sendException = new InvalidOperationException("send failed");
        var disconnectException = new IOException("disconnect failed");
        transport.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(sendException));
        transport.DisconnectAsync(true, CancellationToken.None)
            .Returns(Task.FromException(disconnectException));

        var sut = new TestableSmtpEmailSender(
            logger,
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                Username = "user",
                Password = "pass",
                DefaultFromAddress = "noreply@example.com"
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()),
            transport);

        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        Func<Task> act = () => sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        // Username/Password are set above so the blank-username warning path isn't also exercised —
        // the only Warning-level log expected here is the disconnect-failure one below.
        // The send failure is the actionable one and must reach the caller unchanged, even though
        // cleanup (disconnect) also failed — a naive finally would let the disconnect exception
        // replace it instead.
        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().BeSameAs(sendException);

        logger.ReceivedCalls().Should().ContainSingle(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) &&
            (LogLevel)call.GetArguments()[0]! == LogLevel.Warning);
    }

    [Fact]
    public async Task SendEmailWithBody_MultipleRecipients_AddsAllRecipients()
    {
        // Arrange
        // Inspect inside Arg.Do — the message is disposed by SendEmailAsync's `using` before control
        // returns, so reading it afterward throws ObjectDisposedException.
        var transport = Substitute.For<IMailTransport>();
        int? recipientCount = null;
        string? firstAddress = null;
        string? secondAddress = null;
        transport.SendAsync(Arg.Do<MimeMessage>(msg =>
        {
            recipientCount = msg.To.Count;
            firstAddress = (msg.To[0] as MailboxAddress)?.Address;
            secondAddress = (msg.To[1] as MailboxAddress)?.Address;
        }), Arg.Any<CancellationToken>()).Returns("250 OK");

        var sut = new TestableSmtpEmailSender(
            Substitute.For<ILogger<SmtpEmailSender>>(),
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                DefaultFromAddress = "noreply@example.com"
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()),
            transport);

        var email = new EmailWithBody
        {
            To = "recipient1@example.com,recipient2@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        await sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        recipientCount.Should().Be(2);
        firstAddress.Should().Be("recipient1@example.com");
        secondAddress.Should().Be("recipient2@example.com");
    }

    [Fact]
    public async Task SendEmailWithBody_Port465WithSslDisabled_LogsWarningOnce()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var transport = Substitute.For<IMailTransport>();
        var sut = new TestableSmtpEmailSender(
            logger,
            Options.Create(new SmtpSettings
            {
                Host = "smtp.example.com",
                Port = 465,
                EnableSsl = false,
                Username = "user",
                Password = "pass",
                DefaultFromAddress = "noreply@example.com"
            }),
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()),
            transport);

        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        // Sent twice — with only one send this assertion would pass identically whether the gate
        // works or was deleted, the same reasoning as the blank-username warning test above.
        await sut.SendEmailAsync(email, CancellationToken.None);
        await sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        logger.ReceivedCalls().Should().ContainSingle(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) &&
            (LogLevel)call.GetArguments()[0]! == LogLevel.Warning);
    }

    [Fact]
    public void SmtpSettings_Defaults_CheckCertificateRevocationIsFalse()
    {
        // Arrange & Act
        var settings = new SmtpSettings();

        // Assert
        settings.CheckCertificateRevocation.Should().BeFalse();
    }

    private sealed class TestableSmtpEmailSender(
        ILogger<SmtpEmailSender> logger,
        IOptions<SmtpSettings> options,
        EmailTemplateRenderer templateRenderer,
        IMailTransport transport) : SmtpEmailSender(logger, options, templateRenderer)
    {
        protected override IMailTransport CreateTransport() => transport;
    }
}
