using Endatix.Core.Entities;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public class EmailTemplateTests
{
    [Fact]
    public void Render_WithDoubleBracePlaceholders_ReplacesCorrectly()
    {
        // Arrange
        var template = new EmailTemplate(
            name: "test-template",
            subject: "Hello {{name}}",
            htmlContent: "<p>Welcome {{name}}! Your token is {{verificationToken}}</p>",
            plainTextContent: "Welcome {{name}}! Your token is {{verificationToken}}",
            fromAddress: "noreply@example.com"
        );

        var variables = new Dictionary<string, string>
        {
            ["name"] = "John",
            ["verificationToken"] = "abc123"
        };

        // Act
        var result = template.Render("test@example.com", variables);

        // Assert
        result.Subject.Should().Be("Hello John");
        result.HtmlBody.Should().Be("<p>Welcome John! Your token is abc123</p>");
        result.PlainTextBody.Should().Be("Welcome John! Your token is abc123");
        result.From.Should().Be("noreply@example.com");
        result.To.Should().Be("test@example.com");
    }

    [Fact]
    public void Render_WithHubUrlAndToken_ConstructsVerificationUrl()
    {
        // Arrange
        var template = new EmailTemplate(
            name: "verification-template",
            subject: "Verify Your Email",
            htmlContent: "<p>Click here to verify: <a href=\"{{hubUrl}}/verify-email?token={{verificationToken}}\">Verify Email</a></p>",
            plainTextContent: "Click here to verify: {{hubUrl}}/verify-email?token={{verificationToken}}",
            fromAddress: "noreply@example.com"
        );

        var variables = new Dictionary<string, string>
        {
            ["hubUrl"] = "https://app.example.com",
            ["verificationToken"] = "abc123"
        };

        // Act
        var result = template.Render("test@example.com", variables);

        // Assert
        result.Subject.Should().Be("Verify Your Email");
        result.HtmlBody.Should().Be("<p>Click here to verify: <a href=\"https://app.example.com/verify-email?token=abc123\">Verify Email</a></p>");
        result.PlainTextBody.Should().Be("Click here to verify: https://app.example.com/verify-email?token=abc123");
        result.From.Should().Be("noreply@example.com");
        result.To.Should().Be("test@example.com");
    }

    [Fact]
    public void Render_WithUnusedPlaceholders_LeavesThemUnchanged()
    {
        // Arrange
        var template = new EmailTemplate(
            name: "test-template",
            subject: "Hello {{name}}",
            htmlContent: "<p>Welcome {{name}}! Your token is {{verificationToken}}</p>",
            plainTextContent: "Welcome {{name}}! Your token is {{verificationToken}}",
            fromAddress: "noreply@example.com"
        );

        var variables = new Dictionary<string, string>
        {
            ["name"] = "John"
            // verificationToken not provided
        };

        // Act
        var result = template.Render("test@example.com", variables);

        // Assert
        result.Subject.Should().Be("Hello John");
        result.HtmlBody.Should().Be("<p>Welcome John! Your token is {{verificationToken}}</p>");
        result.PlainTextBody.Should().Be("Welcome John! Your token is {{verificationToken}}");
        result.To.Should().Be("test@example.com");
    }
} 