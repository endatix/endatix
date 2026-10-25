using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Writes to the match-key Property (endatix#1365): it shows the identifier, so a write of the identifier or of nothing
/// stores no cell, and any other value is refused with a message that names the Property, in the participant panel,
/// the designer's presets and the CSV import alike. Every Snapshot path freezes the identifier.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private const string MatchKeyRefusal = "Email is the participant identifier and cannot be changed.";

    [Fact]
    public async Task A_personalised_link_freezes_the_identifier_in_the_match_key_property()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "Email", dataType = "text" });
        await AddParticipantAsync(admin, formId, "ada@example.com");
        var token = await IssueLinkAsync(admin, formId);
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, formId, token);

        // Assert
        var frozen = JsonNode.Parse(await PersonalizationOfAsync(admin, formId, IdOf(opened, "submissionId")))!;
        Assert.Equal("ada@example.com", frozen["variables"]?["email"]?.GetValue<string>());
    }

    [Fact]
    public async Task An_on_behalf_create_freezes_the_identifier_in_the_match_key_property()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "Email", dataType = "text" });
        await AddParticipantAsync(admin, formId, "ada@example.com");

        // Act
        var submissionId = await OnBehalfOfAsync(admin, formId, "ada@example.com");

        // Assert
        var frozen = JsonNode.Parse(await PersonalizationOfAsync(admin, formId, submissionId))!;
        Assert.Equal("ada@example.com", frozen["variables"]?["email"]?.GetValue<string>());
    }

    [Fact]
    public async Task Changing_the_match_key_property_in_the_participant_panel_is_refused_and_the_identifier_stores_nothing()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var email = await admin.CreatePropertyAsync(formId, new { name = "Email", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var personUrl = $"/api/forms/{formId}/audience/people/{ada.MembershipId}";

        // Act
        using var changed = await admin.PutValueAsync(personUrl, email, "other@example.com");
        using var same = await admin.PutValueAsync(personUrl, email, "ada@example.com");
        var participant = await GetParticipantAsync(admin, formId, ada.MemberId);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        Assert.Contains(MatchKeyRefusal, await changed.Content.ReadAsStringAsync(admin.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        Assert.Equal(
            "ada@example.com",
            (await admin.ReadAsync(same)).GetProperty("values").GetProperty($"{email}").GetString());
        Assert.Equal("ada@example.com", participant.GetProperty("values").GetProperty($"{email}").GetString());
        Assert.Empty(await LiveCellsAsync(world.Services, formId));
    }

    [Fact]
    public async Task Editing_the_email_preset_in_the_designer_is_refused_and_the_identifier_stores_nothing()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "Email", dataType = "text" });
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        await AddTestParticipantAsync(admin, formId, "tester@example.com");

        // Act
        using var changed = await SaveTestParticipantsResponseAsync(
            admin, formId, """[{"name":"tester@example.com","variables":{"email":"other@example.com"}}]""");
        var saved = await SaveTestParticipantsAsync(
            admin,
            formId,
            """
            [{"name":"tester@example.com","variables":{"email":"tester@example.com","city":"Varna"}},
             {"name":"new@example.com","variables":{"email":"new@example.com"}}]
            """);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        Assert.Contains(MatchKeyRefusal, await changed.Content.ReadAsStringAsync(admin.CancellationToken));
        Assert.Equal(new Dictionary<long, string> { [city] = "Varna" }, await LiveCellsAsync(world.Services, formId));
        var presets = saved.GetProperty("variablePresets").GetProperty("presets").EnumerateArray()
            .ToDictionary(preset => preset.GetProperty("name").GetString()!, preset => preset.GetProperty("variables"));
        AssertJson("""{"email":"new@example.com"}""", presets["new@example.com"]);
        AssertJson("""{"email":"tester@example.com","city":"Varna"}""", presets["tester@example.com"]);
    }

    [Fact]
    public async Task Importing_into_the_match_key_property_stores_nothing_for_the_identifier_and_rejects_another_value()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "Email", dataType = "text" });
        const string csv = "email,Mail\nada@example.com,ada@example.com\nbob@example.com,other@example.com\n";

        // Act
        using var response = await admin.ImportAsync(formId, csv, new Dictionary<string, string> { ["email"] = "Mail" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await admin.ReadAsync(response);
        var rejection = Assert.Single(result.GetProperty("rejections").EnumerateArray());
        Assert.Equal(3, rejection.GetProperty("rowNumber").GetInt32());
        Assert.Equal(MatchKeyRefusal, rejection.GetProperty("reason").GetString());
        Assert.Empty(await LiveCellsAsync(world.Services, formId));
    }

    [Fact]
    public async Task A_match_key_property_that_is_not_text_keeps_its_stored_values()
    {
        // Arrange
        var admin = await AdminAsync();
        using var key = await admin.PutKeyAsync("external_id");
        Assert.Equal(HttpStatusCode.OK, key.StatusCode);
        var formId = await admin.CreateFormAsync();
        var externalId = await admin.CreatePropertyAsync(formId, new { name = "External ID", dataType = "number" });
        var tester = await AddPersonWithValuesAsync(
            admin, formId, new("007", IsTest: true, new() { [externalId] = "42" }));

        // Act
        var preset = await GetParticipantPresetAsync(admin, formId, tester.MemberId);
        var submissionId = IdOf(await OpenTestSubmissionAsync(admin, formId, tester.MembershipId), "submissionId");
        var frozen = JsonNode.Parse(await PersonalizationOfAsync(admin, formId, submissionId))!["variables"];

        // Assert: the identifier 007 is not typed as the number 7
        AssertJson("""{"external_id":42}""", preset.GetProperty("variables"));
        Assert.Equal(42, frozen?["external_id"]?.GetValue<double>());
    }
}
