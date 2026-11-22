using Endatix.Api.Endpoints.FormSettings;
using Endatix.Core.Common;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.FormSettings;

/// <summary>
/// The webhooks section of a form settings write is held to the rules of the form endpoints that write the same
/// settings: no retired event key, and no endpoint on a private or local network unless the server allows it.
/// </summary>
public sealed class FormSettingsWebHooksValidatorTests
{
    private const string PRIVATE_MESSAGE =
        "A webhook endpoint URL points to a private or local network address. Use a public address.";

    [Fact]
    public void Validate_APrivateEndpoint_FailsWithTheEntityMessage()
    {
        var result = new PartialUpdateFormSettingsValidator().TestValidate(Request("SubmissionCompleted", "http://127.0.0.1:8080/"));

        result.ShouldHaveValidationErrorFor(x => x.WebHooks).WithErrorMessage(PRIVATE_MESSAGE);
    }

    [Fact]
    public void Validate_APrivateEndpoint_WhenTheServerAllowsIt_Passes()
    {
        var validator = new PartialUpdateFormSettingsValidator(new OutboundAddressPolicy(AllowPrivateNetworkTargets: true));

        var result = validator.TestValidate(Request("SubmissionCompleted", "http://127.0.0.1:8080/"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ARelativeEndpoint_Fails()
    {
        var result = new PartialUpdateFormSettingsValidator().TestValidate(Request("SubmissionCompleted", "/hooks"));

        result.ShouldHaveValidationErrorFor(x => x.WebHooks);
    }

    [Fact]
    public void Validate_ARetiredEventKey_FailsWithTheEntityMessage()
    {
        var result = new PartialUpdateFormSettingsValidator()
            .TestValidate(Request("SubmissionCollectionStatusChanged", "https://hooks.example.test/"));

        result.ShouldHaveValidationErrorFor(x => x.WebHooks)
            .Should().ContainSingle().Which.ErrorMessage.Should().Contain("'SubmissionClosed'");
    }

    [Fact]
    public void ToChange_KeepsEveryEndpointField()
    {
        var request = Request("SubmissionCompleted", "https://hooks.example.test/");
        request.WebHooks!.Events["SubmissionCompleted"].WebHookEndpoints[0] = new WebHookEndpointSettingsModel
        {
            Url = "https://hooks.example.test/",
            Authentication = new WebHookAuthSettingsModel { Type = "ApiKey", ApiKey = "key", ApiKeyHeader = "X-Key" },
        };

        var endpoint = request.ToChange().WebHooks!.Events["SubmissionCompleted"].WebHookEndpoints.Single();

        endpoint.Url.Should().Be("https://hooks.example.test/");
        endpoint.Authentication!.ApiKey.Should().Be("key");
        endpoint.Authentication.ApiKeyHeader.Should().Be("X-Key");
        endpoint.Authentication.Type.Should().Be("ApiKey");
    }

    private static PartialUpdateFormSettingsRequest Request(string eventKey, string url) => new()
    {
        FormId = 1,
        WebHooks = new WebHookSettingsModel
        {
            Events = new()
            {
                [eventKey] = new WebHookEventSettingsModel
                {
                    IsEnabled = true,
                    WebHookEndpoints = [new WebHookEndpointSettingsModel { Url = url }],
                },
            },
        },
    };
}
