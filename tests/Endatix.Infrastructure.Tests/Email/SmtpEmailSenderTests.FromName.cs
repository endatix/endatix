using Endatix.Core.Branding;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Email;
using MailKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Email;

public partial class SmtpEmailSenderTests
{
    private static readonly DeploymentSender _sender = new("deployment@example.com", "Example Team", ConfiguredAddress: null);
    private static readonly EmailVariables _variables = EmailVariables.From(DeploymentBrand.BuiltIn);

    [Theory]
    [InlineData(null, "Example Team")]
    [InlineData("Example Forms", "Example Forms")]
    public async Task SendEmailWithBody_FromName_NamesTheSenderOrUsesTheDeploymentSenderName(string? fromName, string expectedName)
    {
        // Arrange
        // Inspect inside Arg.Do: SendEmailAsync disposes the message before returning.
        var transport = Substitute.For<IMailTransport>();
        string? sentName = null;
        string? sentAddress = null;
        transport.SendAsync(Arg.Do<ISendRequest>(request =>
        {
            var sender = request.Message.From.Mailboxes.Single();
            sentName = sender.Name;
            sentAddress = sender.Address;
        }), Arg.Any<CancellationToken>()).Returns(_relayAccepted);

        var sut = SmtpSenderWith(transport);
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

    [Fact]
    public async Task SendEmailWithBody_NoFromAddress_SendsFromTheDeploymentSender()
    {
        // Arrange
        var transport = Substitute.For<IMailTransport>();
        string? sentAddress = null;
        transport.SendAsync(Arg.Do<ISendRequest>(request => sentAddress = request.Message.From.Mailboxes.Single().Address),
            Arg.Any<CancellationToken>()).Returns(_relayAccepted);
        var sut = SmtpSenderWith(transport);
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
        sentAddress.Should().Be("deployment@example.com");
    }

    [Theory]
    [InlineData(null, "Operator Team")]
    [InlineData("Example Forms", "Operator Team")]
    public async Task SendEmailWithBody_FromWithADisplayName_KeepsTheAddressAndItsOwnName(string? fromName, string expectedName)
    {
        // Arrange: an operator FromAddress may already carry a display name.
        var transport = Substitute.For<IMailTransport>();
        string? sentName = null;
        string? sentAddress = null;
        transport.SendAsync(Arg.Do<ISendRequest>(request =>
        {
            var sender = request.Message.From.Mailboxes.Single();
            sentName = sender.Name;
            sentAddress = sender.Address;
        }), Arg.Any<CancellationToken>()).Returns(_relayAccepted);
        var sut = SmtpSenderWith(transport);
        var email = new EmailWithBody
        {
            To = "recipient@example.com",
            From = "Operator Team <sender@example.com>",
            FromName = fromName,
            Subject = "Test Subject",
            PlainTextBody = "Hello World",
            HtmlBody = "<html>Hello World</html>"
        };

        // Act
        await sut.SendEmailAsync(email, CancellationToken.None);

        // Assert
        sentAddress.Should().Be("sender@example.com");
        sentName.Should().Be(expectedName);
    }

    private static TestableSmtpEmailSender SmtpSenderWith(IMailTransport transport) => new(
        Substitute.For<ILogger<SmtpEmailSender>>(),
        Options.Create(new SmtpSettings { Host = "smtp.example.com" }),
        new EmailTemplateRenderer(
            Substitute.For<IRepository<EmailTemplate>>(),
            Options.Create(new EmailTemplateSettings()), _variables, _sender),
        transport);
}
