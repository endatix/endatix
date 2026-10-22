using System.Net;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// One participant's values as a preset, for Preview as on the Audience tab (endatix#1347): any participant on the
/// form, real or test, named by identifier and typed as the variables read types them.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Reading_a_real_participants_preset_returns_their_typed_values_named_by_identifier()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var values = await OnePropertyOfEveryDataTypeAsync(admin, formId);
        var real = await AddPersonWithValuesAsync(admin, formId, new("real@example.com", IsTest: false, values));
        await AddPersonWithValuesAsync(admin, formId, new("other@example.com", IsTest: false, new()));

        // Act
        var preset = await GetParticipantPresetAsync(admin, formId, real.MemberId);

        // Assert
        AssertJson(
            """
            {"name":"real@example.com","variables":{"city":"Sofia","seats":12.5,"consent":true,"joined":"2026-10-21",
             "renews":"2026-10-21T09:30:00+03:00","tier":"enterprise","sites":["b","lab"]}}
            """,
            preset);
    }

    [Fact]
    public async Task A_test_participants_preset_equals_their_preset_in_the_variables_read()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var values = await OnePropertyOfEveryDataTypeAsync(admin, formId);
        var tester = await AddPersonWithValuesAsync(admin, formId, new("tester@example.com", IsTest: true, values));

        // Act
        var preset = await GetParticipantPresetAsync(admin, formId, tester.MemberId);

        // Assert
        var listed = Assert.Single(
            (await GetVariablesAsync(admin, formId)).GetProperty("variablePresets").GetProperty("presets")
                .EnumerateArray());
        AssertJson(listed.GetRawText(), preset);
    }

    [Fact]
    public async Task A_participants_preset_leaves_out_a_deleted_property_and_one_without_a_value()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var seats = await admin.CreatePropertyAsync(formId, new { name = "Seats", dataType = "number" });
        await admin.CreatePropertyAsync(formId, new { name = "Tier", dataType = "text" });
        var ada = await AddPersonWithValuesAsync(
            admin, formId, new("ada@example.com", IsTest: false, new() { [city] = "Sofia", [seats] = "3" }));
        using var deletion = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/properties/{seats}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);

        // Act
        var preset = await GetParticipantPresetAsync(admin, formId, ada.MemberId);

        // Assert
        AssertJson("""{"name":"ada@example.com","variables":{"city":"Sofia"}}""", preset);
    }

    // The variables read is null without Properties; one participant is still found, with no values.
    [Fact]
    public async Task Reading_the_preset_of_a_participant_on_a_form_without_properties_returns_no_values()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var solo = await AddParticipantAsync(admin, formId, "solo@example.com");

        // Act
        var preset = await GetParticipantPresetAsync(admin, formId, solo.MemberId);

        // Assert
        AssertJson("""{"name":"solo@example.com","variables":{}}""", preset);
    }

    [Fact]
    public async Task Reading_the_preset_of_a_participant_on_another_form_is_not_found()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await FormWithCityAsync(admin);
        var otherFormId = await FormWithCityAsync(admin);
        var elsewhere = await AddParticipantAsync(admin, otherFormId, "elsewhere@example.com");

        // Act
        using var response = await GetParticipantPresetResponseAsync(admin, formId, elsewhere.MemberId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_the_preset_of_a_removed_participant_is_not_found()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await FormWithCityAsync(admin);
        var removed = await AddParticipantAsync(admin, formId, "removed@example.com");
        using var removal = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/people/{removed.MembershipId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removal.StatusCode);

        // Act
        using var response = await GetParticipantPresetResponseAsync(admin, formId, removed.MemberId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_participants_preset_on_another_tenants_form_is_not_found()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            TestContext.Current.CancellationToken);
        var owner = await AsAsync(world, TestPersona.TenantAdmin);
        var stranger = new AudienceApi(
            await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: owner.CancellationToken),
            owner.CancellationToken);
        var formId = await FormWithCityAsync(owner);
        var ada = await AddParticipantAsync(owner, formId, "ada@example.com");

        // Act
        using var response = await GetParticipantPresetResponseAsync(stranger, formId, ada.MemberId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_participants_preset_without_forms_edit_is_forbidden()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword, AfterSeed = SeedViewerAsync },
            TestContext.Current.CancellationToken);
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var viewer = new AudienceApi(
            await world.AsAsync(
                TestPersona.CustomRole(ViewerRole), userName: ViewerUserName, cancellationToken: admin.CancellationToken),
            admin.CancellationToken);
        var formId = await FormWithCityAsync(admin);
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");

        // Act
        using var response = await GetParticipantPresetResponseAsync(viewer, formId, ada.MemberId);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static Task<HttpResponseMessage> GetParticipantPresetResponseAsync(
        AudienceApi api, long formId, long memberId) =>
        api.Client.GetAsync(
            $"/api/forms/{formId}/audience/variables/participants/{memberId}", api.CancellationToken);

    private static async Task<JsonElement> GetParticipantPresetAsync(AudienceApi api, long formId, long memberId)
    {
        using var response = await GetParticipantPresetResponseAsync(api, formId, memberId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await api.ReadAsync(response);
    }
}
