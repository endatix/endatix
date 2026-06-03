using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Email;

namespace Endatix.Infrastructure.Tests.Email;

public class SmtpEmailSenderTests
{
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
            new EmailTemplateRenderer(Substitute.For<IRepository<EmailTemplate>>()));

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
            new EmailTemplateRenderer(templateRepository));

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
}
