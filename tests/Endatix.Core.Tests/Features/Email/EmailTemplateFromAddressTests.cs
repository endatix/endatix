using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using FluentAssertions;

namespace Endatix.Core.Tests.Features.Email;

public class EmailTemplateFromAddressTests
{
    private static readonly DeploymentSender NoSender = new("noreply@endatix.com", "Endatix", ConfiguredAddress: null);
    private static readonly DeploymentSender ConfiguredSender = new("sender@example.com", "Endatix", "sender@example.com");

    private static EmailTemplate Row(string name, string fromAddress) => new(name, "Subject", "<p>Body</p>", "Body", fromAddress);

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

        var result = EmailTemplateFromAddress.Resolve(settings, Row("email-verification", "noreply@endatix.com"), NoSender);

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

        var result = EmailTemplateFromAddress.Resolve(settings, Row("forgot-password", "noreply@endatix.com"), NoSender);

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

        var result = EmailTemplateFromAddress.Resolve(settings, Row("email-verification", "noreply@endatix.com"), NoSender);

        result.Should().Be("noreply@endatix.com");
    }

    [Fact]
    public void Resolve_UnknownTemplateUsesDatabaseValue()
    {
        var settings = new EmailTemplateSettings();

        var result = EmailTemplateFromAddress.Resolve(settings, Row("custom-template", "db-sender@example.com"), NoSender);

        result.Should().Be("db-sender@example.com");
    }

    [Fact]
    public void Resolve_ConfiguredSenderAddressWinsOverDatabaseValue()
    {
        var result = EmailTemplateFromAddress.Resolve(
            new EmailTemplateSettings(),
            Row("forgot-password", "noreply@endatix.com"),
            ConfiguredSender);

        result.Should().Be("sender@example.com");
    }

    [Fact]
    public void Resolve_ConfiguredTemplateAddressWinsOverConfiguredSender()
    {
        var settings = new EmailTemplateSettings
        {
            ForgotPasswordEmail = new EmailTemplateConfig { TemplateId = "forgot-password", FromAddress = "reset@example.com" }
        };

        var result = EmailTemplateFromAddress.Resolve(settings, Row("forgot-password", "noreply@endatix.com"), ConfiguredSender);

        result.Should().Be("reset@example.com");
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

        EmailTemplateFromAddress.Resolve(settings, Row("email-verification", "noreply@endatix.com"), NoSender).Should().Be("verify@example.com");

        EmailTemplateFromAddress.Resolve(settings, Row(EmailTemplateSettings.UserInvitationTemplateId, "noreply@endatix.com"), NoSender).Should().Be("invite@example.com");
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

        var result = EmailTemplateFromAddress.Resolve(settings, Row(EmailTemplateSettings.UserInvitationTemplateId, "noreply@endatix.com"), NoSender);

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

        var result = EmailTemplateFromAddress.Resolve(settings, Row(EmailTemplateSettings.UserInvitationTemplateId, "noreply@endatix.com"), NoSender);

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

        EmailTemplateFromAddress.Resolve(settings, Row("email-verification", "db@example.com"), NoSender).Should().Be("db@example.com");
    }
}
