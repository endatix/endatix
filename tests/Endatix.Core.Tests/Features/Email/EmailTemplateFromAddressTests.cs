using Endatix.Core.Configuration;
using Endatix.Core.Features.Email;
using FluentAssertions;

namespace Endatix.Core.Tests.Features.Email;

public class EmailTemplateFromAddressTests
{
    [Fact]
    public void Resolve_ConfiguredAddressOverridesDatabaseValue()
    {
        var settings = new EmailTemplateSettings
        {
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "custom@example.com"
            }
        };

        var result = EmailTemplateFromAddress.Resolve(
            settings,
            "email-verification",
            "noreply@endatix.com");

        result.Should().Be("custom@example.com");
    }

    [Fact]
    public void Resolve_EmptyConfiguredAddressUsesDatabaseValue()
    {
        var settings = new EmailTemplateSettings
        {
            ForgotPasswordEmail = new EmailTemplateConfig
            {
                TemplateId = "forgot-password",
                FromAddress = string.Empty
            }
        };

        var result = EmailTemplateFromAddress.Resolve(
            settings,
            "forgot-password",
            "noreply@endatix.com");

        result.Should().Be("noreply@endatix.com");
    }

    [Fact]
    public void Resolve_WhitespaceConfiguredAddressUsesDatabaseValue()
    {
        var settings = new EmailTemplateSettings
        {
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "   "
            }
        };

        var result = EmailTemplateFromAddress.Resolve(
            settings,
            "email-verification",
            "noreply@endatix.com");

        result.Should().Be("noreply@endatix.com");
    }

    [Fact]
    public void Resolve_UnknownTemplateUsesDatabaseValue()
    {
        var settings = new EmailTemplateSettings();

        var result = EmailTemplateFromAddress.Resolve(
            settings,
            "custom-template",
            "db-sender@example.com");

        result.Should().Be("db-sender@example.com");
    }

    [Fact]
    public void Resolve_NoConfiguredOrDatabaseValueUsesDefault()
    {
        var settings = new EmailTemplateSettings();

        var result = EmailTemplateFromAddress.Resolve(
            settings,
            "custom-template",
            string.Empty);

        result.Should().Be(EmailTemplateFromAddress.DefaultFromAddress);
    }

    [Fact]
    public void Resolve_EmailVerificationAndUserInvitationAreIndependent()
    {
        var settings = new EmailTemplateSettings
        {
            EmailVerification = new EmailTemplateConfig
            {
                TemplateId = "email-verification",
                FromAddress = "verify@example.com"
            },
            UserInvitation = new EmailTemplateConfig
            {
                TemplateId = EmailTemplateSettings.UserInvitationTemplateId,
                FromAddress = "invite@example.com"
            }
        };

        EmailTemplateFromAddress.Resolve(
            settings,
            "email-verification",
            "noreply@endatix.com").Should().Be("verify@example.com");

        EmailTemplateFromAddress.Resolve(
            settings,
            EmailTemplateSettings.UserInvitationTemplateId,
            "noreply@endatix.com").Should().Be("invite@example.com");
    }

    [Fact]
    public void Resolve_UserInvitationConfigAppliesToSeededTemplateName()
    {
        var settings = new EmailTemplateSettings
        {
            UserInvitation = new EmailTemplateConfig
            {
                TemplateId = EmailTemplateSettings.UserInvitationTemplateId,
                FromAddress = "pick@endatix.com"
            }
        };

        var result = EmailTemplateFromAddress.Resolve(
            settings,
            EmailTemplateSettings.UserInvitationTemplateId,
            "noreply@endatix.com");

        result.Should().Be("pick@endatix.com");
    }

    [Fact]
    public void Resolve_CustomInvitationTemplateId_StillOverlaysSeededTemplateName()
    {
        var settings = new EmailTemplateSettings
        {
            UserInvitation = new EmailTemplateConfig
            {
                TemplateId = "d-custom-sendgrid-id",
                FromAddress = "pick@endatix.com"
            }
        };

        var result = EmailTemplateFromAddress.Resolve(
            settings,
            EmailTemplateSettings.UserInvitationTemplateId,
            "noreply@endatix.com");

        result.Should().Be("pick@endatix.com");
    }

    [Fact]
    public void DefaultSettings_OmitFromAddress_SoDatabaseRowIsUsed()
    {
        var settings = new EmailTemplateSettings();

        settings.EmailVerification.FromAddress.Should().BeEmpty();
        settings.UserInvitation.FromAddress.Should().BeEmpty();
        settings.ForgotPasswordEmail.FromAddress.Should().BeEmpty();
        settings.PasswordChangedEmail.FromAddress.Should().BeEmpty();

        EmailTemplateFromAddress.Resolve(
            settings,
            "email-verification",
            "db@example.com").Should().Be("db@example.com");
    }
}
