using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Personalised links over HTTP: issue once per person, the first anonymous open freezes the
/// person's values on a new submission, a repeat open returns it, and the typed read shows them.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Opening_a_personalised_link_twice_returns_one_submission_with_the_frozen_values()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Columbus");
        using var anonymous = world.AnonymousClient();

        // Act
        var first = await RedeemAsync(anonymous, formId, token);
        var second = await RedeemAsync(anonymous, formId, token);

        // Assert
        Assert.True(first.GetProperty("created").GetBoolean());
        Assert.False(second.GetProperty("created").GetBoolean());
        Assert.Equal(IdOf(first, "submissionId"), IdOf(second, "submissionId"));
        Assert.False(first.TryGetProperty("snapshot", out _));
        Assert.False(string.IsNullOrEmpty(first.GetProperty("accessToken").GetString()));
        var submission = await admin.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/{IdOf(first, "submissionId")}", admin.CancellationToken);
        Assert.Equal(
            "Columbus",
            submission.GetProperty("personalization").GetProperty("variables").GetProperty("city").GetString());
    }

    [Fact]
    public async Task Generating_links_again_issues_none_to_people_who_have_one()
    {
        // Arrange
        var admin = await AdminAsync();
        (var formId, _) = await LinkForPersonWithCityAsync(admin, "Dayton");

        // Act
        using var response = await admin.PostAsync($"/api/forms/{formId}/audience/links", new { });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await admin.ReadAsync(response)).GetArrayLength());
    }

    [Fact]
    public async Task Opening_an_unknown_link_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        using var anonymous = world.AnonymousClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(
            $"/api/forms/{formId}/audience/links/{new string('a', 64)}/redeem", new { }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>A form with a City property and one person whose city is set; their one link.</summary>
    private static async Task<(long FormId, string Token)> LinkForPersonWithCityAsync(AudienceApi admin, string city)
    {
        (var personUrl, var propertyId) = await admin.PersonWithPropertyAsync(new { name = "City", dataType = "text" });
        using var saved = await admin.PutValueAsync(personUrl, propertyId, city);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var formId = long.Parse(personUrl.Split('/')[3]);
        using var issued = await admin.PostAsync($"/api/forms/{formId}/audience/links", new { });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var link = Assert.Single((await admin.ReadAsync(issued)).EnumerateArray());
        return (formId, link.GetProperty("token").GetString()!);
    }

    private static async Task<JsonElement> RedeemAsync(HttpClient anonymous, long formId, string token)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var response = await anonymous.PostAsJsonAsync(
            $"/api/forms/{formId}/audience/links/{token}/redeem", new { }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
