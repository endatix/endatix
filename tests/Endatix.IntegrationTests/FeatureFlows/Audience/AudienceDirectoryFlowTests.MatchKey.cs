using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Endatix.IntegrationTests;

/// <summary>
/// The match-key Property (endatix#1365): the form's Property named for the tenant's match key holds each participant's
/// identifier wherever values are read, without being stored, and the identifier wins over a stored value.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task A_participant_added_by_identifier_alone_has_the_match_key_property_filled_everywhere()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var email = await admin.CreatePropertyAsync(formId, new { name = "Email", dataType = "text" });
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");

        // Act
        var variables = await GetVariablesAsync(admin, formId);
        var adaPreset = await GetParticipantPresetAsync(admin, formId, ada.MemberId);
        var people = await ListPeopleAsync(admin, formId, "");
        var submissionId = IdOf(await OpenTestSubmissionAsync(admin, formId, tester.MembershipId), "submissionId");
        var frozen = JsonNode.Parse(await PersonalizationOfAsync(admin, formId, submissionId))!["variables"];

        // Assert
        var testerPreset = variables.GetProperty("variablePresets").GetProperty("presets").EnumerateArray()
            .Single(preset => preset.GetProperty("name").GetString() == "tester@example.com");
        AssertJson("""{"email":"tester@example.com"}""", testerPreset.GetProperty("variables"));
        AssertJson("""{"email":"ada@example.com"}""", adaPreset.GetProperty("variables"));
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(testerPreset.GetProperty("variables").GetRawText()), frozen),
            $"previewed {testerPreset.GetProperty("variables").GetRawText()} != frozen {frozen?.ToJsonString()}");
        AssertJson(
            """
            [{"variableName":"email","filledTest":1,"filledReal":1},
             {"variableName":"city","filledTest":0,"filledReal":0}]
            """,
            variables.GetProperty("coverage").GetProperty("properties"));
        Assert.All(people.GetProperty("items").EnumerateArray(), person =>
        {
            Assert.Equal(1, person.GetProperty("notFilledIn").GetInt32());
            Assert.Equal(
                person.GetProperty("identifier").GetString(),
                person.GetProperty("values").GetProperty($"{email}").GetString());
            Assert.False(person.GetProperty("values").TryGetProperty($"{city}", out _));
        });
    }

    [Fact]
    public async Task The_identifier_wins_over_a_value_stored_for_the_match_key_property()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var email = await admin.CreatePropertyAsync(formId, new { name = "Email", dataType = "text" });
        var tester = await AddPersonWithValuesAsync(
            admin, formId, new("tester@example.com", IsTest: true, new() { [email] = "old@example.com" }));

        // Act
        var preset = await GetParticipantPresetAsync(admin, formId, tester.MemberId);
        var submissionId = IdOf(await OpenTestSubmissionAsync(admin, formId, tester.MembershipId), "submissionId");
        var frozen = JsonNode.Parse(await PersonalizationOfAsync(admin, formId, submissionId))!["variables"];

        // Assert
        AssertJson("""{"email":"tester@example.com"}""", preset.GetProperty("variables"));
        Assert.Equal("tester@example.com", frozen?["email"]?.GetValue<string>());
    }

    [Fact]
    public async Task With_an_external_id_match_key_the_external_id_property_holds_the_identifier()
    {
        // Arrange
        var admin = await AdminAsync();
        using var key = await admin.PutKeyAsync("external_id");
        Assert.Equal(HttpStatusCode.OK, key.StatusCode);
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "External ID", dataType = "text" });
        await admin.CreatePropertyAsync(formId, new { name = "Email", dataType = "text" });
        var person = await AddParticipantAsync(admin, formId, "AbC-1");

        // Act
        var preset = await GetParticipantPresetAsync(admin, formId, person.MemberId);

        // Assert: Email is an ordinary Property under this match key
        AssertJson("""{"external_id":"AbC-1"}""", preset.GetProperty("variables"));
    }

    [Fact]
    public async Task A_form_without_a_match_key_property_reads_only_stored_values()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await FormWithCityAsync(admin);
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");

        // Act
        var preset = await GetParticipantPresetAsync(admin, formId, ada.MemberId);
        var people = await ListPeopleAsync(admin, formId, "");

        // Assert
        AssertJson("{}", preset.GetProperty("variables"));
        Assert.Equal(JsonValueKind.Object, people.GetProperty("items")[0].GetProperty("values").ValueKind);
        Assert.Equal(1, people.GetProperty("items")[0].GetProperty("notFilledIn").GetInt32());
    }
}
