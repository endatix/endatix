using Endatix.Core;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Email;

namespace Endatix.Infrastructure.Tests.Email;

public class EmailTemplateRendererTests
{
    private readonly IRepository<EmailTemplate> _templateRepository;
    private readonly EmailTemplateRenderer _sut;

    public EmailTemplateRendererTests()
    {
        _templateRepository = Substitute.For<IRepository<EmailTemplate>>();
        _sut = new EmailTemplateRenderer(_templateRepository);
    }

    [Fact]
    public async Task RenderAsync_NullEmail_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = () => _sut.RenderAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task RenderAsync_InvalidTo_ThrowsArgumentException(string? to)
    {
        // Arrange
        EmailWithTemplate email = CreateValidEmail(to: to!);

        // Act
        Func<Task> act = () => _sut.RenderAsync(email, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task RenderAsync_InvalidTemplateId_ThrowsArgumentException(string? templateId)
    {
        // Arrange
        EmailWithTemplate email = CreateValidEmail(templateId: templateId!);

        // Act
        Func<Task> act = () => _sut.RenderAsync(email, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RenderAsync_TemplateNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _templateRepository.FirstOrDefaultAsync(Arg.Any<EmailTemplateByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns((EmailTemplate?)null);

        EmailWithTemplate email = CreateValidEmail();

        // Act
        Func<Task> act = () => _sut.RenderAsync(email, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*test-template*");
    }

    [Fact]
    public async Task RenderAsync_WithMetadata_RendersSubjectHtmlAndPlainText()
    {
        // Arrange
        EmailTemplate template = new(
            "test-template",
            "Welcome {{name}}!",
            "<html>Hi {{name}}, your email is {{email}}</html>",
            "Hi {{name}}, your email is {{email}}",
            "template-from@example.com");

        _templateRepository.FirstOrDefaultAsync(Arg.Any<EmailTemplateByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(template);

        EmailWithTemplate email = new()
        {
            To = "recipient@example.com",
            TemplateId = "test-template",
            Metadata = new Dictionary<string, object>
            {
                ["name"] = "Alice",
                ["email"] = "alice@example.com"
            }
        };

        // Act
        EmailWithBody result = await _sut.RenderAsync(email, CancellationToken.None);

        // Assert
        result.To.Should().Be("recipient@example.com");
        result.From.Should().Be("template-from@example.com");
        result.Subject.Should().Be("Welcome Alice!");
        result.HtmlBody.Should().Be("<html>Hi Alice, your email is alice@example.com</html>");
        result.PlainTextBody.Should().Be("Hi Alice, your email is alice@example.com");
    }

    [Fact]
    public async Task RenderAsync_WithSubjectAndFromOverrides_UsesOverrides()
    {
        // Arrange
        EmailTemplate template = CreateTemplate();
        _templateRepository.FirstOrDefaultAsync(Arg.Any<EmailTemplateByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(template);

        EmailWithTemplate email = CreateValidEmail(
            from: "override-from@example.com",
            subject: "Override Subject {{name}}");

        // Act
        EmailWithBody result = await _sut.RenderAsync(email, CancellationToken.None);

        // Assert
        result.From.Should().Be("override-from@example.com");
        result.Subject.Should().Be("Override Subject John");
    }

    private static EmailWithTemplate CreateValidEmail(
        string to = "recipient@example.com",
        string from = "sender@example.com",
        string subject = "Test Subject",
        string templateId = "test-template") => new()
        {
            To = to,
            From = from,
            Subject = subject,
            TemplateId = templateId,
            Metadata = new Dictionary<string, object>
            {
                ["name"] = "John"
            }
        };

    private static EmailTemplate CreateTemplate() => new(
        "test-template",
        "Template Subject",
        "<html>Hello {{name}}</html>",
        "Hello {{name}}",
        "template-from@example.com");
}
