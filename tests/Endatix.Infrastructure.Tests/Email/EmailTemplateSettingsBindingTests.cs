using Endatix.Core.Configuration;
using Endatix.Core.Features.Email;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Email;

public class EmailTemplateSettingsBindingTests
{
    [Fact]
    public void AddEmailTemplateSettings_UserInvitationSection_BindsFromAddressAndTemplateId()
    {
        var settings = BindEmailTemplateSettings(new Dictionary<string, string?>
        {
            ["Endatix:EmailTemplates:UserInvitation:TemplateId"] = EmailTemplateSettings.UserInvitationTemplateId,
            ["Endatix:EmailTemplates:UserInvitation:FromAddress"] = "pick@endatix.com"
        });

        settings.UserInvitation.TemplateId.Should().Be(EmailTemplateSettings.UserInvitationTemplateId);
        settings.UserInvitation.FromAddress.Should().Be("pick@endatix.com");

        var fromAddress = EmailTemplateFromAddress.Resolve(
            settings,
            EmailTemplateSettings.UserInvitationTemplateId,
            "noreply@endatix.com");

        fromAddress.Should().Be("pick@endatix.com");
    }

    [Fact]
    public void AddEmailTemplateSettings_CustomInvitationTemplateId_IsPreserved()
    {
        var settings = BindEmailTemplateSettings(new Dictionary<string, string?>
        {
            ["Endatix:EmailTemplates:UserInvitation:TemplateId"] = "d-custom-sendgrid-id",
            ["Endatix:EmailTemplates:UserInvitation:FromAddress"] = "pick@endatix.com"
        });

        settings.UserInvitation.TemplateId.Should().Be("d-custom-sendgrid-id");
        settings.UserInvitation.FromAddress.Should().Be("pick@endatix.com");
    }

    private static EmailTemplateSettings BindEmailTemplateSettings(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddEmailTemplateSettings();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<EmailTemplateSettings>>().Value;
    }
}
