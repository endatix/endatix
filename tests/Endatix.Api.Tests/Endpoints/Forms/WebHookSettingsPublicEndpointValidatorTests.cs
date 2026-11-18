using System.Text.Json;
using Endatix.Api.Endpoints.Forms;
using Endatix.Core.Common;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Tests.Endpoints.Forms;

/// <summary>A form webhook-settings write whose endpoint URL names a private or local host is a 400, unless the server allows it.</summary>
public class WebHookSettingsPublicEndpointValidatorTests
{
    private const string Message = "A webhook endpoint URL points to a private or local network address. Use a public address.";
    private const string PrivateJson = """{"Events":{"SubmissionCompleted":{"IsEnabled":true,"WebHookEndpoints":[{"Url":"http://127.0.0.1:8080/"}]}}}""";
    private static readonly OutboundAddressPolicy AnyAddress = new(AllowPrivateNetworkTargets: true);

    [Fact]
    public void Validate_UpdateWithPrivateEndpoint_FailsWithTheEntityMessage()
    {
        // Arrange
        var request = new UpdateFormRequest { FormId = 1, Name = "Form", IsEnabled = true, WebHookSettingsJson = PrivateJson };

        // Act
        var result = new UpdateFormValidator().TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.WebHookSettingsJson).WithErrorMessage(Message);
    }

    [Fact]
    public void Validate_PartialUpdateWithPrivateEndpoint_Fails()
    {
        // Arrange
        var request = new PartialUpdateFormRequest { FormId = 1, WebHookSettingsJson = PrivateJson };

        // Act
        var result = new PartialUpdateFormValidator().TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.WebHookSettingsJson).WithErrorMessage(Message);
    }

    [Fact]
    public void Validate_CreateWithPrivateEndpointInTheTypedField_Fails()
    {
        // Arrange
        var request = CreateRequest();
        request.WebHookSettings = JsonDocument.Parse(PrivateJson).RootElement;

        // Act
        var result = new CreateFormValidator().TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.WebHookSettings).WithErrorMessage(Message);
    }

    [Fact]
    public void Validate_CreateWithPrivateEndpointInTheStringField_Fails()
    {
        // Arrange
        var request = CreateRequest();
#pragma warning disable CS0618 // The obsolete string field is still accepted, so its rule is tested.
        request.WebHookSettingsJson = PrivateJson;

        // Act
        var result = new CreateFormValidator().TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.WebHookSettingsJson).WithErrorMessage(Message);
#pragma warning restore CS0618
    }

    [Fact]
    public void Validate_PrivateTargetsAllowed_PassesOnEveryPath()
    {
        // Arrange
        var create = CreateRequest();
        create.WebHookSettings = JsonDocument.Parse(PrivateJson).RootElement;
        var update = new UpdateFormRequest { FormId = 1, Name = "Form", IsEnabled = true, WebHookSettingsJson = PrivateJson };
        var patch = new PartialUpdateFormRequest { FormId = 1, WebHookSettingsJson = PrivateJson };

        // Act
        var created = new CreateFormValidator(AnyAddress).TestValidate(create);
        var updated = new UpdateFormValidator(AnyAddress).TestValidate(update);
        var patched = new PartialUpdateFormValidator(AnyAddress).TestValidate(patch);

        // Assert
        created.ShouldNotHaveValidationErrorFor(x => x.WebHookSettings);
        updated.ShouldNotHaveValidationErrorFor(x => x.WebHookSettingsJson);
        patched.ShouldNotHaveValidationErrorFor(x => x.WebHookSettingsJson);
    }

    // FastEndpoints builds validators with ActivatorUtilities. With the server's policy registered, each form validator is
    // built through the constructor that takes it, so a server that allows private targets accepts them.
    [Fact]
    public void ActivatorUtilities_PolicyRegistered_BuildsEachValidatorWithTheServersPolicy()
    {
        // Arrange
        using var provider = new ServiceCollection().AddSingleton(AnyAddress).BuildServiceProvider();
        var create = CreateRequest();
        create.WebHookSettings = JsonDocument.Parse(PrivateJson).RootElement;
        var update = new UpdateFormRequest { FormId = 1, Name = "Form", IsEnabled = true, WebHookSettingsJson = PrivateJson };
        var patch = new PartialUpdateFormRequest { FormId = 1, WebHookSettingsJson = PrivateJson };

        // Act
        var created = ActivatorUtilities.CreateInstance<CreateFormValidator>(provider).TestValidate(create);
        var updated = ActivatorUtilities.CreateInstance<UpdateFormValidator>(provider).TestValidate(update);
        var patched = ActivatorUtilities.CreateInstance<PartialUpdateFormValidator>(provider).TestValidate(patch);

        // Assert
        created.ShouldNotHaveValidationErrorFor(x => x.WebHookSettings);
        updated.ShouldNotHaveValidationErrorFor(x => x.WebHookSettingsJson);
        patched.ShouldNotHaveValidationErrorFor(x => x.WebHookSettingsJson);
    }

    [Fact]
    public void Validate_UpdateWithAnFtpEndpoint_FailsEvenWhenPrivateTargetsAreAllowed()
    {
        // Arrange
        var request = new UpdateFormRequest
        {
            FormId = 1,
            Name = "Form",
            IsEnabled = true,
            WebHookSettingsJson = PrivateJson.Replace("http://127.0.0.1:8080/", "ftp://files.example.com/"),
        };

        // Act
        var result = new UpdateFormValidator(AnyAddress).TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.WebHookSettingsJson)
            .WithErrorMessage("A webhook endpoint URL must be an absolute http or https URL.");
    }

    [Fact]
    public void Validate_PublicEndpoint_Passes()
    {
        // Arrange
        var request = new UpdateFormRequest
        {
            FormId = 1,
            Name = "Form",
            IsEnabled = true,
            WebHookSettingsJson = PrivateJson.Replace("http://127.0.0.1:8080/", "https://hooks.example.com/"),
        };

        // Act
        var result = new UpdateFormValidator(OutboundAddressPolicy.PublicOnly).TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.WebHookSettingsJson);
    }

    private static CreateFormRequest CreateRequest() => new()
    {
        Name = "Form",
        IsEnabled = true,
        FormDefinitionSchema = JsonDocument.Parse("""{"pages":[]}""").RootElement,
    };
}
