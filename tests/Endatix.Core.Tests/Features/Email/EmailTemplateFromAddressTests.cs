using Endatix.Core.Configuration;
using Endatix.Core.Features.Email;
using FluentAssertions;

namespace Endatix.Core.Tests.Features.Email;

public class EmailTemplateFromAddressTests
{
    [Fact]
    public void ResolveEffectiveFromAddress_ConfiguredAddressOverridesDatabaseValue()
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "custom@example.com"
            }
        };

        // Act
        var result = EmailTemplateFromAddress.ResolveEffectiveFromAddress(
            settings,
            "email-verification",
            "noreply@endatix.com");

        // Assert
        result.Should().Be("custom@example.com");
    }

    [Fact]
    public void ResolveEffectiveFromAddress_EmptyConfiguredAddressUsesDatabaseValue()
    {
        // Arrange
        var settings = new EmailTemplateSettings
        {
            ForgotPasswordEmail = new EmailTemplateConfig
            {
                TemplateId = "forgot-password",
                FromAddress = string.Empty
            }
        };

        // Act
        var result = EmailTemplateFromAddress.ResolveEffectiveFromAddress(
            settings,
            "forgot-password",
            "noreply@endatix.com");

        // Assert
        result.Should().Be("noreply@endatix.com");
    }

    [Fact]
    public void ResolveEffectiveFromAddress_UnknownTemplateUsesDatabaseValue()
    {
        // Arrange
        var settings = new EmailTemplateSettings();

        // Act
        var result = EmailTemplateFromAddress.ResolveEffectiveFromAddress(
            settings,
            "custom-template",
            "db-sender@example.com");

        // Assert
        result.Should().Be("db-sender@example.com");
    }

    [Fact]
    public void ResolveEffectiveFromAddress_NoConfiguredOrDatabaseValueUsesDefault()
    {
        // Arrange
        var settings = new EmailTemplateSettings();

        // Act
        var result = EmailTemplateFromAddress.ResolveEffectiveFromAddress(
            settings,
            "custom-template",
            string.Empty);

        // Assert
        result.Should().Be(EmailTemplateFromAddress.DefaultFromAddress);
    }
}
