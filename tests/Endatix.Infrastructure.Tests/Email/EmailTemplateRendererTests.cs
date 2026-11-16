using Endatix.Core;
using Endatix.Core.Branding;
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

        var sut = new EmailTemplateRenderer(repository, settings, DeploymentBrand.BuiltIn);

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

        var sut = new EmailTemplateRenderer(repository, settings, DeploymentBrand.BuiltIn);

        var result = await sut.RenderAsync(
            new EmailWithTemplate
            {
                To = "user@example.com",
                TemplateId = "email-verification"
            },
            TestContext.Current.CancellationToken);

        result.From.Should().Be("db@example.com");
    }

    private static readonly DeploymentBrand PackBrand = new(
        ProductName: "Example Forms",
        AppName: "Example Forms",
        AssistantName: "Example Assistant",
        WebsiteUrl: "https://example.com",
        Email: new BrandEmail("Example Forms Team", "pack@example.com", "help@example.com"));

    private static EmailTemplateRenderer RendererFor(EmailTemplate template, DeploymentBrand brand, string configuredFromAddress = "")
    {
        var repository = Substitute.For<IRepository<EmailTemplate>>();
        repository
            .FirstOrDefaultAsync(Arg.Any<EmailTemplateByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(template);
        var settings = Options.Create(new EmailTemplateSettings
        {
            EmailVerification = new EmailTemplateConfig { TemplateId = "email-verification", FromAddress = configuredFromAddress }
        });
        return new EmailTemplateRenderer(repository, settings, brand);
    }

    private static EmailTemplate BrandedTemplate() => new(
        "email-verification",
        "Welcome to {{productName}}",
        "<p>{{productName}}: {{supportAddress}} {{websiteUrl}} {{senderName}}</p>",
        "{{productName}}: {{supportAddress}}",
        "db@example.com");

    [Fact]
    public async Task RenderAsync_PackBrand_FillsTheBrandVariables()
    {
        // Arrange
        var sut = RendererFor(BrandedTemplate(), PackBrand);

        // Act
        var result = await sut.RenderAsync(
            new EmailWithTemplate { To = "user@example.com", TemplateId = "email-verification" },
            TestContext.Current.CancellationToken);

        // Assert
        result.Subject.Should().Be("Welcome to Example Forms");
        result.HtmlBody.Should().Be("<p>Example Forms: help@example.com https://example.com Example Forms Team</p>");
        result.PlainTextBody.Should().Be("Example Forms: help@example.com");
    }

    [Fact]
    public async Task RenderAsync_CallerMetadataWithABrandKey_WinsOverTheBrand()
    {
        // Arrange
        var sut = RendererFor(BrandedTemplate(), PackBrand);

        // Act
        var result = await sut.RenderAsync(
            new EmailWithTemplate
            {
                To = "user@example.com",
                TemplateId = "email-verification",
                Metadata = new Dictionary<string, object> { ["productName"] = "Caller Product" }
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.Subject.Should().Be("Welcome to Caller Product");
    }

    [Fact]
    public async Task RenderAsync_PackBrand_SetsTheSenderNameAndAddress()
    {
        // Arrange
        var sut = RendererFor(BrandedTemplate(), PackBrand);

        // Act
        var result = await sut.RenderAsync(
            new EmailWithTemplate { To = "user@example.com", TemplateId = "email-verification" },
            TestContext.Current.CancellationToken);

        // Assert
        result.FromName.Should().Be("Example Forms Team");
        result.From.Should().Be("pack@example.com", "the pack's address wins over the template row");
    }

    [Fact]
    public async Task RenderAsync_OperatorConfiguredFromAddress_WinsOverThePack()
    {
        // Arrange
        var sut = RendererFor(BrandedTemplate(), PackBrand, configuredFromAddress: "operator@example.com");

        // Act
        var result = await sut.RenderAsync(
            new EmailWithTemplate { To = "user@example.com", TemplateId = "email-verification" },
            TestContext.Current.CancellationToken);

        // Assert
        result.From.Should().Be("operator@example.com");
    }

    [Fact]
    public async Task RenderAsync_BuiltInBrand_KeepsTheRowAddressAndSetsNoSenderName()
    {
        // Arrange
        var sut = RendererFor(BrandedTemplate(), DeploymentBrand.BuiltIn);

        // Act
        var result = await sut.RenderAsync(
            new EmailWithTemplate { To = "user@example.com", TemplateId = "email-verification" },
            TestContext.Current.CancellationToken);

        // Assert
        result.From.Should().Be("db@example.com");
        result.FromName.Should().BeNull();
        result.Subject.Should().Be("Welcome to Endatix");
    }
}
