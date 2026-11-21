using Endatix.Core;
using Endatix.Core.Branding;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
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

        var sut = new EmailTemplateRenderer(repository, settings, BuiltInVariables, BuiltInSender);

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

        var sut = new EmailTemplateRenderer(repository, settings, BuiltInVariables, BuiltInSender);

        var result = await sut.RenderAsync(
            new EmailWithTemplate
            {
                To = "user@example.com",
                TemplateId = "email-verification"
            },
            TestContext.Current.CancellationToken);

        result.From.Should().Be("db@example.com");
    }

    private static readonly EmailVariables BuiltInVariables = EmailVariables.From(DeploymentBrand.BuiltIn);
    private static readonly DeploymentSender BuiltInSender = new("noreply@endatix.com", "Endatix", ConfiguredAddress: null);

    private static readonly EmailVariables PackVariables = new(
        ProductName: "Example Forms",
        AppName: "Example Forms",
        SupportAddress: "help@example.com",
        WebsiteUrl: "https://example.com");

    private static readonly DeploymentSender ConfiguredSender = new("sender@example.com", "Example Forms Team", "sender@example.com");

    // Renders BrandedTemplate().
    private static EmailTemplateRenderer RendererFor(
        EmailVariables variables,
        DeploymentSender sender,
        string configuredFromAddress = "")
    {
        var template = BrandedTemplate();
        var repository = Substitute.For<IRepository<EmailTemplate>>();
        repository
            .FirstOrDefaultAsync(Arg.Any<EmailTemplateByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(template);
        var settings = Options.Create(new EmailTemplateSettings
        {
            EmailVerification = new EmailTemplateConfig { TemplateId = "email-verification", FromAddress = configuredFromAddress }
        });
        return new EmailTemplateRenderer(repository, settings, variables, sender);
    }

    private static EmailTemplate BrandedTemplate() => new(
        "email-verification",
        "Welcome to {{productName}}",
        "<p>{{productName}}: {{supportAddress}} {{websiteUrl}}</p>",
        "{{productName}}: {{supportAddress}}",
        "db@example.com");

    [Fact]
    public async Task RenderAsync_PackVariables_FillsTheBrandVariables()
    {
        // Arrange
        var sut = RendererFor(PackVariables, ConfiguredSender);

        // Act
        var result = await sut.RenderAsync(
            new EmailWithTemplate { To = "user@example.com", TemplateId = "email-verification" },
            TestContext.Current.CancellationToken);

        // Assert
        result.Subject.Should().Be("Welcome to Example Forms");
        result.HtmlBody.Should().Be("<p>Example Forms: help@example.com https://example.com</p>");
        result.PlainTextBody.Should().Be("Example Forms: help@example.com");
    }

    [Fact]
    public async Task RenderAsync_CallerMetadataWithABrandKey_WinsOverTheBrand()
    {
        // Arrange
        var sut = RendererFor(PackVariables, ConfiguredSender);

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
    public async Task RenderAsync_ConfiguredDeploymentSender_SendsFromItWithItsName()
    {
        // Arrange
        var sut = RendererFor(PackVariables, ConfiguredSender);

        // Act
        var result = await sut.RenderAsync(
            new EmailWithTemplate { To = "user@example.com", TemplateId = "email-verification" },
            TestContext.Current.CancellationToken);

        // Assert
        result.FromName.Should().Be("Example Forms Team");
        result.From.Should().Be("sender@example.com", "a configured sender address wins over the template row");
    }

    [Fact]
    public async Task RenderAsync_OperatorConfiguredFromAddress_WinsOverTheConfiguredSender()
    {
        // Arrange
        var sut = RendererFor(PackVariables, ConfiguredSender, configuredFromAddress: "operator@example.com");

        // Act
        var result = await sut.RenderAsync(
            new EmailWithTemplate { To = "user@example.com", TemplateId = "email-verification" },
            TestContext.Current.CancellationToken);

        // Assert
        result.From.Should().Be("operator@example.com");
    }

    [Fact]
    public async Task RenderAsync_BuiltInBrand_SendsAsEndatixFromTheRowAddress()
    {
        // Arrange
        var sut = RendererFor(BuiltInVariables, BuiltInSender);

        // Act
        var result = await sut.RenderAsync(
            new EmailWithTemplate { To = "user@example.com", TemplateId = "email-verification" },
            TestContext.Current.CancellationToken);

        // Assert
        result.From.Should().Be("db@example.com");
        result.FromName.Should().Be("Endatix");
        result.Subject.Should().Be("Welcome to Endatix");
    }

    [Fact]
    public void ProviderTemplateData_CallerMetadata_KeepsItsValuesAndAddsTheVariables()
    {
        // Arrange
        var sut = RendererFor(PackVariables, ConfiguredSender);

        // Act
        var data = sut.ProviderTemplateData(new EmailWithTemplate
        {
            To = "user@example.com",
            TemplateId = "d-provider-template",
            IsExternal = true,
            Metadata = new Dictionary<string, object> { ["count"] = 3 }
        });

        // Assert
        data.Should().Contain("count", 3).And.Contain("productName", "Example Forms");
    }
}
