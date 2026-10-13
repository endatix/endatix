using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// A personalised link on a one-response form: the link's response and an on-behalf one are the person's single
/// response, whichever path writes first.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Opening_a_one_response_form_uses_the_existing_on_behalf_submission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Columbus", limitOnePerUser: true);
        using var created = await OnBehalfAsync(admin, link.FormId, link.Identifier);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = IdOf(await admin.ReadAsync(created), "id");
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, link.FormId, link.Token);
        var read = await ReadSubmissionAsync(admin, link.FormId, submissionId);

        // Assert
        Assert.False(opened.GetProperty("created").GetBoolean());
        Assert.Equal(submissionId, IdOf(opened, "submissionId"));
        Assert.Equal("on_behalf", read.GetProperty("personalization").GetProperty("source").GetString());
        Assert.Equal("Columbus", read.GetProperty("personalization").GetProperty("variables").GetProperty("city").GetString());
        Assert.Equal("in_progress", read.GetProperty("collectionStatus").GetString());
    }

    /// <summary>
    /// The on-behalf create ran before the person was in the audience, so it has no values and did not bind the
    /// person. The link still finds it through the person's Submitter, and gives it the values it lacks.
    /// </summary>
    [Fact]
    public async Task Opening_a_link_personalises_the_on_behalf_submission_made_before_the_person_was_added()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync(limitOnePerUser: true);
        var propertyId = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var identifier = $"{Guid.NewGuid():N}@example.com";
        using var created = await OnBehalfAsync(admin, formId, identifier);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = IdOf(await admin.ReadAsync(created), "id");
        var token = await AddPersonInAkronAndIssueLinkAsync(admin, (formId, propertyId), identifier);
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, formId, token);
        var read = await ReadSubmissionAsync(admin, formId, submissionId);

        // Assert
        Assert.Equal(submissionId, IdOf(opened, "submissionId"));
        Assert.Equal("link", read.GetProperty("personalization").GetProperty("source").GetString());
        Assert.Equal("Akron", read.GetProperty("personalization").GetProperty("variables").GetProperty("city").GetString());
        Assert.Equal("in_progress", read.GetProperty("collectionStatus").GetString());
        Assert.NotEqual(JsonValueKind.Null, read.GetProperty("startedAt").ValueKind);
    }

    [Fact]
    public async Task Creating_on_behalf_after_the_link_opened_names_the_link_submission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Dayton", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, link.FormId, link.Token);

        // Act
        using var onBehalf = await OnBehalfAsync(admin, link.FormId, link.Identifier);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, onBehalf.StatusCode);
        Assert.Equal(IdOf(opened, "submissionId"), IdOf(await admin.ReadAsync(onBehalf), "submissionId"));
    }

    [Fact]
    public async Task Opening_the_new_link_of_a_re_added_person_resumes_their_submission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Toledo", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, link.FormId, link.Token);
        using var removed = await admin.Client.DeleteAsync(link.PersonUrl, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var newToken = await AddPersonInAkronAndIssueLinkAsync(admin, (link.FormId, link.PropertyId), link.Identifier);

        // Act
        var again = await RedeemAsync(anonymous, link.FormId, newToken);

        // Assert
        Assert.False(again.GetProperty("created").GetBoolean());
        Assert.Equal(IdOf(opened, "submissionId"), IdOf(again, "submissionId"));
    }

    private static async Task<string> AddPersonInAkronAndIssueLinkAsync(
        AudienceApi admin,
        (long FormId, long PropertyId) cityProperty,
        string identifier)
    {
        using var added = await admin.AddPersonAsync(cityProperty.FormId, identifier);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var personUrl = $"/api/forms/{cityProperty.FormId}/audience/people/{IdOf(await admin.ReadAsync(added), "membershipId")}";
        using var saved = await admin.PutValueAsync(personUrl, cityProperty.PropertyId, "Akron");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        return await IssueLinkAsync(admin, cityProperty.FormId);
    }

    private static Task<JsonElement> ReadSubmissionAsync(AudienceApi admin, long formId, long submissionId) =>
        admin.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/{submissionId}", admin.CancellationToken);
}
