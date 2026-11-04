using Endatix.Api.Endpoints.Forms;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Forms;

/// <summary>A form webhook-settings write that still uses the retired <c>SubmissionCollectionStatusChanged</c> key is a 400.</summary>
public class WebHookSettingsRetiredKeyValidatorTests
{
    private const string RetiredKeyJson =
        """{"Events":{"SubmissionCollectionStatusChanged":{"IsEnabled":true,"WebHookEndpoints":[{"Url":"https://hooks.example.test"}]}}}""";

    private const string NewKeyJson =
        """{"Events":{"SubmissionClosed":{"IsEnabled":true,"WebHookEndpoints":[{"Url":"https://hooks.example.test"}]}}}""";

    [Fact]
    public void Validate_UpdateWithRetiredKey_FailsNamingTheNewKeyAndTheHub()
    {
        // Arrange
        var request = new UpdateFormRequest { FormId = 1, Name = "Form", IsEnabled = true, WebHookSettingsJson = RetiredKeyJson };

        // Act
        var result = new UpdateFormValidator().TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.WebHookSettingsJson)
            .WithErrorMessage("The webhook setting 'SubmissionCollectionStatusChanged' was renamed to 'SubmissionClosed'. Use Endatix Hub 0.9.1 or later, which writes the new setting.");
    }

    [Fact]
    public void Validate_UpdateWithNewKey_Passes()
    {
        // Arrange
        var request = new UpdateFormRequest { FormId = 1, Name = "Form", IsEnabled = true, WebHookSettingsJson = NewKeyJson };

        // Act
        var result = new UpdateFormValidator().TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.WebHookSettingsJson);
    }

    [Fact]
    public void Validate_PartialUpdateWithRetiredKey_Fails()
    {
        // Arrange
        var request = new PartialUpdateFormRequest { FormId = 1, WebHookSettingsJson = RetiredKeyJson };

        // Act
        var result = new PartialUpdateFormValidator().TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.WebHookSettingsJson);
    }
}
