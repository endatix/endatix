using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// The customer's go-live path: a backend creates a submission on behalf of a BusinessId, takes an
/// access token for it, and the respondent's embed reads the submission by that token. The person's
/// audience values are frozen on the submission and come back typed on every read.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Creating_on_behalf_of_a_person_freezes_their_values_for_the_access_token_read()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var (formId, email) = await FormWithPersonAsync(admin, "Columbus");
        using var anonymous = world.AnonymousClient();

        // Act
        using var created = await OnBehalfAsync(admin, formId, email);
        var submissionId = IdOf(await admin.ReadAsync(created), "id");
        var token = await AccessTokenAsync(admin, formId, submissionId);
        var read = await anonymous.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/by-access-token/{token}", admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var personalization = read.GetProperty("personalization");
        Assert.Equal("on_behalf", personalization.GetProperty("source").GetString());
        Assert.Equal("Columbus", personalization.GetProperty("variables").GetProperty("city").GetString());
    }

    [Fact]
    public async Task Creating_on_behalf_of_an_unknown_business_id_creates_a_submission_without_personalization()
    {
        // Arrange
        var admin = await AdminAsync();
        var (formId, _) = await FormWithPersonAsync(admin, "Dayton");

        // Act
        using var created = await OnBehalfAsync(admin, formId, $"{Guid.NewGuid():N}@example.com");

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await admin.ReadAsync(created);
        Assert.True(
            !body.TryGetProperty("personalization", out var personalization)
            || personalization.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task A_second_call_on_a_one_per_user_form_returns_the_existing_submission_id()
    {
        // Arrange
        var admin = await AdminAsync();
        var (formId, email) = await FormWithPersonAsync(admin, "Toledo", limitOnePerUser: true);
        using var first = await OnBehalfAsync(admin, formId, email);
        var firstId = IdOf(await admin.ReadAsync(first), "id");

        // Act
        using var second = await OnBehalfAsync(admin, formId, email);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await admin.ReadAsync(second);
        Assert.Equal("submission_exists", problem.GetProperty("errorCode").GetString());
        Assert.Equal(firstId, IdOf(problem, "submissionId"));
        Assert.False(string.IsNullOrEmpty(await AccessTokenAsync(admin, formId, firstId)));
    }

    [Fact]
    public async Task Parallel_calls_on_a_one_per_user_form_create_one_submission_and_name_it_to_the_other()
    {
        // Arrange
        var admin = await AdminAsync();
        var (formId, email) = await FormWithPersonAsync(admin, "Akron", limitOnePerUser: true);

        // Act
        var responses = await Task.WhenAll(OnBehalfAsync(admin, formId, email), OnBehalfAsync(admin, formId, email));

        // Assert
        foreach (var r in responses)
        { if ((int)r.StatusCode >= 400 && r.StatusCode != HttpStatusCode.Conflict) Assert.Fail(await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); }
        try
        {
            var created = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(
                IdOf(await admin.ReadAsync(created), "id"),
                IdOf(await admin.ReadAsync(conflict), "submissionId"));
        }
        finally
        {
            DisposeAll(responses);
        }
    }

    /// <summary>A form with a City property and one person (by email) whose city is set.</summary>
    private static async Task<(long FormId, string Email)> FormWithPersonAsync(
        AudienceApi admin,
        string city,
        bool limitOnePerUser = false)
    {
        var formId = await admin.CreateFormAsync(limitOnePerUser);
        var propertyId = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var email = $"{Guid.NewGuid():N}@example.com";
        using var added = await admin.AddPersonAsync(formId, email);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var personUrl = $"/api/forms/{formId}/audience/people/{IdOf(await admin.ReadAsync(added), "membershipId")}";
        using var saved = await admin.PutValueAsync(personUrl, propertyId, city);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        return (formId, email);
    }

    private static Task<HttpResponseMessage> OnBehalfAsync(AudienceApi admin, long formId, string businessId) =>
        admin.PostAsync(
            $"/api/forms/{formId}/submissions/onbehalf",
            new { submitter = new { externalSubjectId = businessId, displayId = businessId, authProvider = "Integration" } });

    private static async Task<string> AccessTokenAsync(AudienceApi admin, long formId, long submissionId)
    {
        using var response = await admin.PostAsync(
            $"/api/forms/{formId}/submissions/{submissionId}/access-token",
            new { expiryMinutes = 60, permissions = new[] { "view", "edit" } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await admin.ReadAsync(response)).GetProperty("token").GetString()!;
    }
}
