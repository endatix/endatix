using Endatix.Core;
using Endatix.Core.Features.Email;
using Endatix.Infrastructure.Email;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Email;

public class FakeEmailSenderTests
{
    [Fact]
    public void ProviderMetadata_ReturnsConfiguredFakeProvider()
    {
        var sut = new FakeEmailSender(Substitute.For<ILogger<FakeEmailSender>>());

        sut.ProviderName.Should().Be("Fake");
        sut.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task SendEmailAsync_DoesNotLogRecipientSenderOrVerificationSecret()
    {
        var logger = Substitute.For<ILogger<FakeEmailSender>>();
        var sut = new FakeEmailSender(logger);
        const string to = "person@example.com";
        const string from = "from@example.com";
        const string secret = "verification-secret-value";

        await sut.SendEmailAsync(new EmailWithBody
        {
            To = to,
            From = from,
            Subject = "Verify",
            PlainTextBody = secret,
            HtmlBody = secret
        }, TestContext.Current.CancellationToken);
        await sut.SendEmailAsync(new EmailWithTemplate
        {
            To = to,
            From = from,
            Subject = "Verify",
            TemplateId = "email-verification",
            Metadata = new Dictionary<string, object> { ["verificationToken"] = secret }
        }, TestContext.Current.CancellationToken);

        var logged = string.Join('\n', logger.ReceivedCalls().Select(call =>
            string.Join(' ', call.GetArguments().Select(argument => argument?.ToString()))));

        logged.Should().NotContain(to);
        logged.Should().NotContain(from);
        logged.Should().NotContain(secret);
        logged.Should().NotContain("[redacted-email]");
    }
}
