using System.Net;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Email;

namespace Endatix.Infrastructure.Tests.Email;

public class MailgunEmailSenderTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MailgunEmailSender> _logger;
    private readonly IOptions<MailgunSettings> _options;
    private readonly IRepository<EmailTemplate> _templateRepository;
    private readonly CapturingHttpMessageHandler _handler;
    private readonly MailgunEmailSender _sut;

    public MailgunEmailSenderTests()
    {
        _handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(_handler);
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient().Returns(httpClient);
        _logger = Substitute.For<ILogger<MailgunEmailSender>>();
        _options = Options.Create(new MailgunSettings
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://api.mailgun.net",
            Domain = "test.example.com"
        });
        _templateRepository = Substitute.For<IRepository<EmailTemplate>>();
        _sut = new MailgunEmailSender(
            _httpClientFactory,
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

    private static Dictionary<string, string> ParseFormContent(string content)
    {
        return content.Split('&')
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => HttpUtility.UrlDecode(parts[0]),
                parts => parts.Length > 1 ? HttpUtility.UrlDecode(parts[1]) : "");
    }

    // -- SendEmailAsync(EmailWithBody) guard tests --

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

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SendEmailWithBody_InvalidPlainTextBody_ThrowsArgumentException(string? plainTextBody)
    {
        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            From = "sender@example.com",
            Subject = "Test Subject",
            PlainTextBody = plainTextBody!,
            HtmlBody = "<html>Hello World</html>"
        };

        Func<Task> act = () => _sut.SendEmailAsync(email, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SendEmailWithBody_InvalidHtmlBody_ThrowsArgumentException(string? htmlBody)
    {
        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            From = "sender@example.com",
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = htmlBody!
        };

        Func<Task> act = () => _sut.SendEmailAsync(email, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // -- SendEmailAsync(EmailWithBody) success tests --

    [Fact]
    public async Task SendEmailWithBody_ValidEmail_SendsHttpPostWithCorrectContent()
    {
        var email = CreateValidEmailWithBody();

        await _sut.SendEmailAsync(email, CancellationToken.None);

        _handler.Request.Should().NotBeNull();
        _handler.Request!.Method.Should().Be(HttpMethod.Post);
        _handler.Request.RequestUri!.ToString().Should().Contain("api.mailgun.net/test.example.com/messages");

        var form = ParseFormContent(_handler.RequestContent!);
        form["to"].Should().Be("recipient@example.com");
        form["from"].Should().Be("sender@example.com");
        form["subject"].Should().Be("Test Subject");
        form["text"].Should().Be("Hello World");
        form["html"].Should().Be("<html>Hello World</html>");
    }

    // -- SendEmailAsync(EmailWithTemplate) provider transport tests --

    [Fact]
    public async Task SendEmailWithTemplate_ValidTemplate_RendersAndSendsRenderedContent()
    {
        var template = CreateTestTemplate();
        _templateRepository.FirstOrDefaultAsync(Arg.Any<EmailTemplateByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(template);
        var email = CreateValidEmailWithTemplate();

        await _sut.SendEmailAsync(email, CancellationToken.None);

        await _templateRepository.Received(1).FirstOrDefaultAsync(
            Arg.Is<EmailTemplateByNameSpec>(spec => true),
            Arg.Any<CancellationToken>());
        _handler.Request.Should().NotBeNull();
        var form = ParseFormContent(_handler.RequestContent!);
        form["to"].Should().Be("recipient@example.com");
        form["from"].Should().Be("sender@example.com");
        form["subject"].Should().Be("Test Subject");
        form["text"].Should().Be("Hello John");
        form["html"].Should().Be("<html>Hello John</html>");
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestContent = request.Content is not null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
