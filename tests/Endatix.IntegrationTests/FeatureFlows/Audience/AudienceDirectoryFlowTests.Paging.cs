using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Endatix.IntegrationTests;

/// <summary>
/// The people list's paging over HTTP. Shares the world and API helpers of <see cref="AudienceDirectoryFlowTests"/>.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private const string FivePeopleCsv =
        "email\np1@example.com\np2@example.com\np3@example.com\np4@example.com\np5@example.com\n";

    [Fact]
    public async Task Listing_people_past_the_last_page_returns_the_last_page()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await FormWithFivePeopleAsync(api);
        JsonElement everyone = await api.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/audience/people?pageSize=5", api.CancellationToken);

        // Act
        JsonElement page = await api.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/audience/people?page=99&pageSize=2", api.CancellationToken);

        // Assert
        Assert.Equal(3, page.GetProperty("page").GetInt32());
        Assert.Equal(5, page.GetProperty("totalRecords").GetInt32());
        Assert.Equal(3, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(
            [Identifiers(everyone)[^1]],
            Identifiers(page));
    }

    [Fact]
    public async Task Listing_people_at_page_zero_is_rejected()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await FormWithFivePeopleAsync(api);

        // Act
        using HttpResponseMessage response = await api.Client.GetAsync(
            $"/api/forms/{formId}/audience/people?page=0&pageSize=2", api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Listing_people_with_a_page_size_over_five_thousand_is_rejected()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await FormWithFivePeopleAsync(api);

        // Act
        using HttpResponseMessage response = await api.Client.GetAsync(
            $"/api/forms/{formId}/audience/people?pageSize=5001", api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<long> FormWithFivePeopleAsync(AudienceApi api)
    {
        long formId = await api.CreateFormAsync();
        using HttpResponseMessage imported = await api.ImportAsync(formId, FivePeopleCsv);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        return formId;
    }

    private static List<string> Identifiers(JsonElement page) =>
        page.GetProperty("items").EnumerateArray()
            .Select(person => person.GetProperty("identifier").GetString()!)
            .ToList();
}
