using Endatix.Core;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Email;

public class EmailTemplateRendererTests
{
    [Fact]
    public async Task RenderAsync_ConfiguredFromAddressOverridesDatabaseValue()
    {
        var template = new EmailTemplate(
            "email-verification",
            "Verify your email",
            "<p>Verify {{hubUrl}}</p>",
            "Verify {{hubUrl}}",
            "noreply@endatix.com");

        var repository = Substitute.For<IRepository<EmailTemplate>>();
        repository
            .FirstOrDefaultAsync(Arg.Any<EmailTemplateByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(template);

        var settings = Options.Create(new EmailTemplateSettings
        {
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "custom@example.com"
            }
        });

        var sut = new EmailTemplateRenderer(repository, settings);

        var result = await sut.RenderAsync(
            new EmailWithTemplate
            {
                To = "user@example.com",
                TemplateId = "email-verification",
                Metadata = new Dictionary<string, object> { ["hubUrl"] = "https://app.example.com" }
            },
            TestContext.Current.CancellationToken);

        result.From.Should().Be("custom@example.com");
        result.To.Should().Be("user@example.com");
        result.Subject.Should().Be("Verify your email");
        result.HtmlBody.Should().Be("<p>Verify https://app.example.com</p>");
    }

    [Fact]
    public async Task RenderAsync_EmptyConfiguredFromAddressUsesDatabaseValue()
    {
        var template = new EmailTemplate(
            "email-verification",
            "Verify your email",
            "<p>Verify</p>",
            "Verify",
            "db@example.com");

        var repository = Substitute.For<IRepository<EmailTemplate>>();
        repository
            .FirstOrDefaultAsync(Arg.Any<EmailTemplateByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(template);

        var settings = Options.Create(new EmailTemplateSettings
        {
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "   "
            }
        });

        var sut = new EmailTemplateRenderer(repository, settings);

        var result = await sut.RenderAsync(
            new EmailWithTemplate
            {
                To = "user@example.com",
                TemplateId = "email-verification"
            },
            TestContext.Current.CancellationToken);

        result.From.Should().Be("db@example.com");
    }
}
