using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Email;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Endatix.Infrastructure.Tests.Email;

public class SendGridEmailSenderTests
{
    private readonly ISendGridClient _sendGridClient;
    private readonly ILogger<SendGridEmailSender> _logger;
    private readonly IOptions<SendGridSettings> _options;
    private readonly IRepository<EmailTemplate> _templateRepository;
    private readonly SendGridEmailSender _sut;

    public SendGridEmailSenderTests()
    {
        _sendGridClient = Substitute.For<ISendGridClient>();
        _logger = Substitute.For<ILogger<SendGridEmailSender>>();
        _options = Options.Create(new SendGridSettings
        {
            ApiKey = "test-api-key"
        });
        _templateRepository = Substitute.For<IRepository<EmailTemplate>>();
        _sut = new SendGridEmailSender(
            _sendGridClient,
            _logger,
            _options,
            new EmailTemplateRenderer(_templateRepository));
    }

    private static EmailWithBody CreateValidEmailWithBody() => new()
    {
        To = "recipient@example.com",
        From = "sender@example.com",
        Subject = "Test Subject",
        PlainTextBody = "Hello World",
        HtmlBody = "<html>Hello World</html>"
    };

    private static EmailWithTemplate CreateValidEmailWithTemplate() => new()
    {
        To = "recipient@example.com",
        From = "sender@example.com",
        Subject = "Test Subject",
        TemplateId = "test-template",
        Metadata = new Dictionary<string, object>
        {
            ["name"] = "John"
        }
    };

    private static EmailTemplate CreateTestTemplate() => new(
        "test-template",
        "Template Subject",
        "<html>Hello {{name}}</html>",
        "Hello {{name}}",
        "template-from@example.com");

    private static Response CreateSuccessResponse() =>
        new(HttpStatusCode.OK, new StringContent("{}"), null!);

    // -- SendEmailAsync(EmailWithBody) guard tests --

    [Fact]
    public async Task SendEmailWithBody_NullEmail_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.SendEmailAsync((EmailWithBody)null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SendEmailWithBody_InvalidTo_ThrowsArgumentException(string? to)
    {
        var email = new EmailWithBody
        {
            To = to!,
            From = "sender@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        Func<Task> act = () => _sut.SendEmailAsync(email, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SendEmailWithBody_InvalidFrom_ThrowsArgumentException(string? from)
    {
        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            From = from,
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        Func<Task> act = () => _sut.SendEmailAsync(email, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SendEmailWithBody_InvalidSubject_ThrowsArgumentException(string? subject)
    {
        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            From = "sender@example.com",
            Subject = subject,
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        Func<Task> act = () => _sut.SendEmailAsync(email, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // -- SendEmailAsync(EmailWithBody) success tests --

    [Fact]
    public void ProviderMetadata_ConfiguredSettings_ReturnsSendGrid()
    {
        _sut.ProviderName.Should().Be("SendGrid");
        _sut.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task SendEmailWithBody_ValidEmail_SendsViaSendGrid()
    {
        _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResponse());
        var email = CreateValidEmailWithBody();

        await _sut.SendEmailAsync(email, CancellationToken.None);

        await _sendGridClient.Received(1).SendEmailAsync(
            Arg.Is<SendGridMessage>(msg =>
                msg.From.Email == "sender@example.com" &&
                msg.Personalizations[0].Tos[0].Email == "recipient@example.com"),
            Arg.Any<CancellationToken>());
    }

    // -- SendEmailAsync(EmailWithTemplate) provider transport tests --

    [Fact]
    public async Task SendEmailWithTemplate_ValidTemplate_RendersAndSendsViaSendGrid()
    {
        var template = CreateTestTemplate();
        _templateRepository.FirstOrDefaultAsync(Arg.Any<EmailTemplateByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(template);
        _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResponse());
        var email = CreateValidEmailWithTemplate();

        await _sut.SendEmailAsync(email, CancellationToken.None);

        await _templateRepository.Received(1).FirstOrDefaultAsync(
            Arg.Is<EmailTemplateByNameSpec>(spec => true),
            Arg.Any<CancellationToken>());
        await _sendGridClient.Received(1).SendEmailAsync(
            Arg.Is<SendGridMessage>(msg =>
                msg.From.Email == "sender@example.com" &&
                msg.Personalizations[0].Subject == "Test Subject"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailWithTemplate_ExternalTemplate_SendsProviderTemplatePayload()
    {
        SendGridMessage? sentMessage = null;
        _sendGridClient
            .SendEmailAsync(Arg.Do<SendGridMessage>(msg => sentMessage = msg), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResponse());

        var email = new EmailWithTemplate
        {
            To = "recipient@example.com",
            From = "sender@example.com",
            TemplateId = "sendgrid-template",
            IsExternal = true,
            Metadata = new Dictionary<string, object>
            {
                ["name"] = "Alice",
                ["attempt"] = 2
            }
        };

        await _sut.SendEmailAsync(email, CancellationToken.None);

        await _templateRepository.DidNotReceive().FirstOrDefaultAsync(
            Arg.Any<EmailTemplateByNameSpec>(),
            Arg.Any<CancellationToken>());

        sentMessage.Should().NotBeNull();
        sentMessage!.From.Email.Should().Be("sender@example.com");
        sentMessage.Personalizations[0].Tos[0].Email.Should().Be("recipient@example.com");
        sentMessage.TemplateId.Should().Be("sendgrid-template");
        sentMessage.Personalizations[0].TemplateData.Should().BeEquivalentTo(email.Metadata);
    }

}
