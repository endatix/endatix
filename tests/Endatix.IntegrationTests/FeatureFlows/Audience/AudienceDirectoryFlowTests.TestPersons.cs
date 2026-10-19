using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Endatix.IntegrationTests;

/// <summary>
/// Test persons over HTTP: created or imported as test, filtered in the people list, and never
/// issued a personalised link. Shares the world and API helpers of <see cref="AudienceDirectoryFlowTests"/>.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Adding_a_test_person_returns_it_as_test()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        using HttpResponseMessage created = await AddTestPersonAsync(api, formId, "tester@example.com");

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True((await api.ReadAsync(created)).GetProperty("isTest").GetBoolean());
    }

    [Theory]
    [InlineData("test", new[] { "tester@example.com" })]
    [InlineData("real", new[] { "real@example.com" })]
    [InlineData("all", new[] { "real@example.com", "tester@example.com" })]
    public async Task Listing_people_by_test_filter_returns_and_counts_only_that_set(string filter, string[] expected)
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await FormWithRealAndTestPersonAsync(api);

        // Act
        JsonElement page = await ListPeopleAsync(api, formId, $"?test={filter}");

        // Assert
        Assert.Equal(expected, Identifiers(page).Order());
        Assert.Equal(expected.Length, page.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Listing_people_without_a_test_filter_returns_everyone_with_their_flag()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await FormWithRealAndTestPersonAsync(api);

        // Act
        JsonElement page = await ListPeopleAsync(api, formId, string.Empty);

        // Assert
        Assert.Equal(
            new Dictionary<string, bool> { ["real@example.com"] = false, ["tester@example.com"] = true },
            TestFlags(page));
    }

    [Fact]
    public async Task Listing_people_with_an_unknown_test_filter_is_rejected()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        using HttpResponseMessage response = await api.Client.GetAsync(
            $"/api/forms/{formId}/audience/people?test=maybe", api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["test"], await FailingFieldsAsync(api, response));
    }

    [Fact]
    public async Task Importing_as_test_marks_only_people_new_to_the_form_as_test()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        using HttpResponseMessage real = await api.AddPersonAsync(formId, "real@example.com");

        // Act
        using HttpResponseMessage imported = await api.PostAsync(
            $"/api/forms/{formId}/audience/import",
            new
            {
                csvText = "email\nreal@example.com\nnew@example.com\n",
                identifierColumn = "email",
                fileName = "testers.csv",
                isTest = true,
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        JsonElement summary = await api.ReadAsync(imported);
        Assert.Equal(1, summary.GetProperty("createdCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("skippedCount").GetInt32());
        Assert.Equal(
            new Dictionary<string, bool> { ["new@example.com"] = true, ["real@example.com"] = false },
            TestFlags(await ListPeopleAsync(api, formId, string.Empty)));
    }

    [Fact]
    public async Task Generating_links_issues_none_to_test_persons()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        using HttpResponseMessage real = await api.AddPersonAsync(formId, "real@example.com");
        using HttpResponseMessage tester = await AddTestPersonAsync(api, formId, "tester@example.com");
        long realMembershipId = IdOf(await api.ReadAsync(real), "membershipId");

        // Act
        using HttpResponseMessage response = await api.PostAsync($"/api/forms/{formId}/audience/links", new { });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement link = Assert.Single((await api.ReadAsync(response)).EnumerateArray());
        Assert.Equal(realMembershipId, IdOf(link, "membershipId"));
    }

    [Fact]
    public async Task Removing_a_test_person_leaves_the_real_people()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        using HttpResponseMessage real = await api.AddPersonAsync(formId, "real@example.com");
        using HttpResponseMessage tester = await AddTestPersonAsync(api, formId, "tester@example.com");
        long testerMembershipId = IdOf(await api.ReadAsync(tester), "membershipId");

        // Act
        using HttpResponseMessage removed = await api.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/people/{testerMembershipId}", api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(["real@example.com"], Identifiers(await ListPeopleAsync(api, formId, string.Empty)));
    }

    private static async Task<long> FormWithRealAndTestPersonAsync(AudienceApi api)
    {
        long formId = await api.CreateFormAsync();
        using HttpResponseMessage real = await api.AddPersonAsync(formId, "real@example.com");
        using HttpResponseMessage tester = await AddTestPersonAsync(api, formId, "tester@example.com");
        Assert.Equal(HttpStatusCode.Created, real.StatusCode);
        Assert.Equal(HttpStatusCode.Created, tester.StatusCode);
        return formId;
    }

    private static Task<HttpResponseMessage> AddTestPersonAsync(AudienceApi api, long formId, string identifier) =>
        api.PostAsync($"/api/forms/{formId}/audience/people", new { identifier, isTest = true });

    private static Task<JsonElement> ListPeopleAsync(AudienceApi api, long formId, string query) =>
        api.Client.GetFromJsonAsync<JsonElement>($"/api/forms/{formId}/audience/people{query}", api.CancellationToken);

    private static Dictionary<string, bool> TestFlags(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().ToDictionary(
            person => person.GetProperty("identifier").GetString()!,
            person => person.GetProperty("isTest").GetBoolean());
}
