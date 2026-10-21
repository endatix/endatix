using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// A form's Audience variables for the designer (endatix#1345): its Properties as a SurveyJS variable definition and
/// one preset per Test participant, typed exactly as that participant's next test submission freezes them.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task A_form_without_properties_has_no_variables()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await AddTestParticipantAsync(admin, formId, "tester@example.com");

        // Act
        var variables = await GetVariablesAsync(admin, formId);

        // Assert
        Assert.Equal(JsonValueKind.Null, variables.GetProperty("variablePresets").ValueKind);
        AssertJson("""{"test":0,"real":0}""", variables.GetProperty("coverage").GetProperty("participants"));
        Assert.Empty(variables.GetProperty("coverage").GetProperty("properties").EnumerateArray());
        AssertJson("""{"allowAdd":false,"allowEdit":false,"allowDelete":false}""", variables.GetProperty("editing"));
    }

    [Fact]
    public async Task Variables_define_each_property_and_preset_each_test_participant_by_identifier()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var tier = await admin.CreatePropertyAsync(
            formId, new { name = "Plan tier", dataType = "single_choice", choicesJson = """["basic","pro"]""" });
        var renews = await admin.CreatePropertyAsync(formId, new { name = "Renews at", dataType = "date_time" });
        await AddPersonWithValuesAsync(admin, formId, new("real@example.com", IsTest: false, new() { [tier] = "pro" }));
        await AddPersonWithValuesAsync(admin, formId, new("zed@example.com", IsTest: true, new() { [tier] = "basic" }));
        await AddPersonWithValuesAsync(
            admin, formId, new("ada@example.com", IsTest: true, new() { [renews] = "2026-10-21T23:30-05:00" }));

        // Act
        var variables = await GetVariablesAsync(admin, formId);

        // Assert
        var presets = variables.GetProperty("variablePresets");
        AssertJson(
            """
            {"elements":[
              {"type":"dropdown","name":"plan_tier","title":"Plan tier","choices":["basic","pro"]},
              {"type":"text","name":"renews_at","title":"Renews at"}
            ]}
            """,
            presets.GetProperty("definition"));
        AssertJson(
            """
            [
              {"name":"ada@example.com","variables":{"renews_at":"2026-10-21T23:30-05:00"}},
              {"name":"zed@example.com","variables":{"plan_tier":"basic"}}
            ]
            """,
            presets.GetProperty("presets"));
    }

    [Fact]
    public async Task A_previewed_value_equals_the_value_frozen_on_the_next_test_submission_for_every_data_type()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var values = await OnePropertyOfEveryDataTypeAsync(admin, formId);
        var tester = await AddPersonWithValuesAsync(admin, formId, new("tester@example.com", IsTest: true, values));

        // Act
        var previewed = Assert.Single(
            (await GetVariablesAsync(admin, formId)).GetProperty("variablePresets").GetProperty("presets")
                .EnumerateArray()).GetProperty("variables");
        var submissionId = IdOf(await OpenTestSubmissionAsync(admin, formId, tester.MembershipId), "submissionId");
        var frozen = JsonNode.Parse(await PersonalizationOfAsync(admin, formId, submissionId))!["variables"];

        // Assert
        AssertJson(
            """
            {"city":"Sofia","seats":12.5,"consent":true,"joined":"2026-10-21","renews":"2026-10-21T09:30:00+03:00",
             "tier":"enterprise","sites":["b","lab"]}
            """,
            previewed);
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(previewed.GetRawText()), frozen),
            $"previewed {previewed.GetRawText()} != frozen {frozen?.ToJsonString()}");
    }

    [Fact]
    public async Task Choices_that_allow_other_accept_free_text_in_the_definition()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(
            formId, new { name = "Sites", dataType = "multiple_choice", choicesJson = """["a"]""", allowsOther = true });

        // Act
        var variables = await GetVariablesAsync(admin, formId);

        // Assert
        AssertJson(
            """{"elements":[{"type":"tagbox","name":"sites","title":"Sites","choices":["a"],"allowCustomChoices":true}]}""",
            variables.GetProperty("variablePresets").GetProperty("definition"));
    }

    [Fact]
    public async Task Reading_variables_of_another_tenants_form_is_not_found()
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

        // Act
        using var response = await GetVariablesResponseAsync(stranger, formId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_variables_without_forms_edit_is_forbidden()
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

        // Act
        using var response = await GetVariablesResponseAsync(viewer, formId);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A value for each property, keyed by property id, as stored.</summary>
    private static async Task<Dictionary<long, string>> OnePropertyOfEveryDataTypeAsync(AudienceApi admin, long formId) =>
        new()
        {
            [await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" })] = "Sofia",
            [await admin.CreatePropertyAsync(formId, new { name = "Seats", dataType = "number" })] = "12.5",
            [await admin.CreatePropertyAsync(formId, new { name = "Consent", dataType = "boolean" })] = "true",
            [await admin.CreatePropertyAsync(formId, new { name = "Joined", dataType = "date" })] = "2026-10-21",
            [await admin.CreatePropertyAsync(formId, new { name = "Renews", dataType = "date_time" })] =
                "2026-10-21T09:30:00+03:00",
            [await admin.CreatePropertyAsync(
                formId,
                new { name = "Tier", dataType = "single_choice", choicesJson = """["basic"]""", allowsOther = true })] =
                "enterprise",
            [await admin.CreatePropertyAsync(
                formId,
                new { name = "Sites", dataType = "multiple_choice", choicesJson = """["a","b"]""", allowsOther = true })] =
                """["b","lab"]""",
        };

    private sealed record PersonWithValues(string Identifier, bool IsTest, Dictionary<long, string> Values);

    private static async Task<Participant> AddPersonWithValuesAsync(AudienceApi admin, long formId, PersonWithValues person)
    {
        using var added = await admin.PostAsync(
            $"/api/forms/{formId}/audience/people",
            new
            {
                identifier = person.Identifier,
                isTest = person.IsTest,
                values = person.Values.ToDictionary(cell => $"{cell.Key}", cell => cell.Value),
            });
        return await ParticipantOfAsync(admin, added, person.Identifier);
    }

    private static Task<HttpResponseMessage> GetVariablesResponseAsync(AudienceApi api, long formId) =>
        api.Client.GetAsync($"/api/forms/{formId}/audience/variables", api.CancellationToken);

    private static async Task<JsonElement> GetVariablesAsync(AudienceApi api, long formId)
    {
        using var response = await GetVariablesResponseAsync(api, formId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await api.ReadAsync(response);
    }

    private static void AssertJson(string expected, JsonElement actual) =>
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual.GetRawText())),
            $"expected {expected}, got {actual.GetRawText()}");
}
