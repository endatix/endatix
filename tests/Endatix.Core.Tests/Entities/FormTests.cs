using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class FormTests
{
    [Fact]
    public void AddFormDefinition_WhenFirstDefinition_SetsItAsActive()
    {
        // Arrange & Act
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1);
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        form.AddFormDefinition(formDefinition);

        // Assert
        form?.ActiveDefinition.Should().NotBeNull();
        form?.FormDefinitions.Should().HaveCount(1);
        form?.ActiveDefinition?.Should().Be(form?.FormDefinitions.First());
    }

    [Fact]
    public void SetActiveFormDefinition_WhenChangingActive_UpdatesActiveDefinitionCorrectly()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1);
        var formDefinition1 = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        var formDefinition2 = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_2);
        form.AddFormDefinition(formDefinition1);
        form.AddFormDefinition(formDefinition2);

        // Act
        form.SetActiveFormDefinition(formDefinition2);

        // Assert
        form.ActiveDefinition.Should().Be(formDefinition2);
    }

    [Fact]
    public void SetActiveFormDefinition_WithNonExistingDefinition_ThrowsException()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1);
        var externalForm = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_2);
        var externalDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        externalForm.AddFormDefinition(externalDefinition);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => form.SetActiveFormDefinition(externalDefinition)
        );
        Assert.Contains("doesn't belong to this form", exception.Message);
    }

    [Fact]
    public void UpdateWebHookSettings_WithValidConfiguration_UpdatesSettings()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1);
        var webHookConfig = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new WebHookEventConfig
                {
                    IsEnabled = true,
                    WebHookEndpoints = new List<WebHookEndpointConfig>
                    {
                        new WebHookEndpointConfig { Url = "https://api.example.com/webhook" }
                    }
                }
            }
        };

        // Act
        form.UpdateWebHookSettings(webHookConfig);

        // Assert
        form.WebHookSettings.Should().NotBeNull();
        form.WebHookSettings.Events.Should().ContainKey("SubmissionCompleted");
        form.WebHookSettings.Events["SubmissionCompleted"].IsEnabled.Should().BeTrue();
        form.WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints.Should().HaveCount(1);
        form.WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints[0].Url.Should().Be("https://api.example.com/webhook");
    }

    [Fact]
    public void UpdateWebHookSettings_WithNull_ClearsSettings()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1);
        var webHookConfig = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new WebHookEventConfig { IsEnabled = true }
            }
        };
        form.UpdateWebHookSettings(webHookConfig);

        // Act
        form.UpdateWebHookSettings(null);

        // Assert
        form.WebHookSettingsJson.Should().BeNull();
        form.WebHookSettings.Should().NotBeNull(); // Returns empty config
        form.WebHookSettings.Events.Should().BeEmpty();
    }

    [Fact]
    public void WebHookSettings_WhenNoSettingsSet_ReturnsEmptyConfiguration()
    {
        // Arrange & Act
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1);

        // Assert
        form.WebHookSettings.Should().NotBeNull();
        form.WebHookSettings.Events.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithWebHookSettingsJson_DeserializesCorrectly()
    {
        // Arrange
        var webHookJson = """
        {
            "Events": {
                "SubmissionCompleted": {
                    "IsEnabled": true,
                    "WebHookEndpoints": [
                        {
                            "Url": "https://api.example.com/webhook"
                        }
                    ]
                }
            }
        }
        """;

        // Act
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, webHookSettingsJson: webHookJson);

        // Assert
        form.WebHookSettings.Should().NotBeNull();
        form.WebHookSettings.Events.Should().ContainKey("SubmissionCompleted");
        form.WebHookSettings.Events["SubmissionCompleted"].IsEnabled.Should().BeTrue();
        form.WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints.Should().HaveCount(1);
    }
}
