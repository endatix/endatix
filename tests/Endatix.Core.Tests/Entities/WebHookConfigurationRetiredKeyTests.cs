using Endatix.Core.Entities;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public class WebHookConfigurationRetiredKeyTests
{
    [Fact]
    public void UpdateWebHookSettings_FormWithRetiredKey_ThrowsAndKeepsTheStoredSettings()
    {
        // Arrange
        var form = Form.Create(new FormCreateArgs(TenantId: 1, Name: "Form"));
        var settings = new WebHookConfiguration { Events = { ["SubmissionCollectionStatusChanged"] = new WebHookEventConfig { IsEnabled = true } } };

        // Act
        var update = () => form.UpdateWebHookSettings(settings);

        // Assert
        update.Should().Throw<ArgumentException>().WithMessage("*renamed to 'SubmissionClosed'*");
        form.WebHookSettingsJson.Should().BeNull();
    }

    [Fact]
    public void UpdateWebHookSettings_TenantWithRetiredKey_Throws()
    {
        // Arrange
        var tenantSettings = new TenantSettings(1);
        var settings = new WebHookConfiguration { Events = { ["SubmissionCollectionStatusChanged"] = new WebHookEventConfig() } };

        // Act
        var update = () => tenantSettings.UpdateWebHookSettings(settings);

        // Assert
        update.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("""{"Events":{"SubmissionClosed":{}}}""")]
    public void RetiredKeyError_NoRetiredKey_IsNull(string? json)
    {
        // Act
        var error = WebHookConfiguration.RetiredKeyError(json);

        // Assert
        error.Should().BeNull();
    }
}
