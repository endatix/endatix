using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Endatix.IntegrationTests;

/// <summary>
/// What an import does with an empty cell and with a Property the file leaves out (<c>emptyCells</c>,
/// <c>missingProperties</c>), and the paged read of every rejected row. Shares the world and API helpers of
/// <see cref="AudienceDirectoryFlowTests"/>.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task A_one_request_import_keeps_the_stored_value_of_an_empty_cell()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long city = await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        Dictionary<string, string> columns = new() { ["city"] = "City" };
        using HttpResponseMessage first = await api.ImportAsync(formId, "email,City\nada@example.com,Sofia\n", columns);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Act
        using HttpResponseMessage second = await api.ImportAsync(formId, "email,City\nada@example.com,\n", columns);

        // Assert
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, (await api.ReadAsync(second)).GetProperty("skippedCount").GetInt32());
        Assert.Equal("Sofia", (await api.ValuesAsync(formId, "ada@example.com")).GetValueOrDefault(city));
    }

    private sealed partial class AudienceApi
    {
        /// <summary>The stored values of the participant with <paramref name="identifier"/>, by Property id.</summary>
        public async Task<Dictionary<long, string>> ValuesAsync(long formId, string identifier)
        {
            JsonElement page = await client.GetFromJsonAsync<JsonElement>(
                $"/api/forms/{formId}/audience/people?pageSize=5000", cancellationToken);
            JsonElement person = page.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("identifier").GetString() == identifier);
            return person.GetProperty("values").EnumerateObject()
                .ToDictionary(value => long.Parse(value.Name), value => value.Value.GetString()!);
        }
    }
}
