using Endatix.Core.Branding;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Email;
using MailKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Endatix.Infrastructure.Tests.Email;

public partial class SmtpEmailSenderTests
{
    [Theory]
    [InlineData(null, "Endatix")]
    [InlineData("Example Forms", "Example Forms")]
    public async Task SendEmailWithBody_FromName_NamesTheSenderOrKeepsTheConfiguredDefault(string? fromName, string expectedName)
    {
        // Arrange
        // Inspect inside Arg.Do: SendEmailAsync disposes the message before returning.
        var transport = Substitute.For<IMailTransport>();
        string? sentName = null;
        string? sentAddress = null;
        transport.SendAsync(Arg.Do<MimeMessage>(msg =>
        {
            var sender = msg.From.Mailboxes.Single();
            sentName = sender.Name;
            sentAddress = sender.Address;
        }), Arg.Any<CancellationToken>()).Returns("250 OK");

        var sut = SenderNamed("Endatix", transport);
        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            From = "sender@example.com",
            FromName = fromName,
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        await sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        sentName.Should().Be(expectedName);
        sentAddress.Should().Be("sender@example.com");
    }

    private static TestableSmtpEmailSender SenderNamed(string defaultFromName, IMailTransport transport) => new(
        Substitute.For<ILogger<SmtpEmailSender>>(),
        Options.Create(new SmtpSettings
        {
            Host = "smtp.example.com",
            DefaultFromAddress = "noreply@example.com",
            DefaultFromName = defaultFromName
        }),
        new EmailTemplateRenderer(
            Substitute.For<IRepository<EmailTemplate>>(),
            Options.Create(new EmailTemplateSettings()), DeploymentBrand.BuiltIn),
        transport);
}
