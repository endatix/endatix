using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Endatix.IntegrationTests;

/// <summary>
/// The CSV import over HTTP: matching, rejected rows, re-imports, the match-key lock and parallel
/// imports. Shares the world and API helpers of <see cref="AudienceDirectoryFlowTests"/>.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Importing_an_existing_email_in_other_casing_matches_that_person()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        (long formId, _) = await api.AddPersonToNewFormAsync("Ada@Example.com");

        // Act
        using HttpResponseMessage response = await api.ImportAsync(formId, "email\nada@example.com\ncy@example.com\n");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement result = await api.ReadAsync(response);
        // The row has no values, so the matched person is counted as skipped, not created.
        Assert.Equal(1, result.GetProperty("skippedCount").GetInt32());
        Assert.Equal(1, result.GetProperty("createdCount").GetInt32());
    }

    [Fact]
    public async Task Importing_bad_rows_rejects_them_with_their_row_numbers()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        await api.CreatePropertyAsync(formId, new { name = "Age", dataType = "number" });
        const string csv = "email,Age\nada@example.com,36\nJohn Smith,40\nbob@example.com,forty\n";

        // Act
        using HttpResponseMessage response = await api.ImportAsync(
            formId, csv, new Dictionary<string, string> { ["age"] = "Age" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        int[] rejectedRows = (await api.ReadAsync(response)).GetProperty("rejections").EnumerateArray()
            .Select(rejection => rejection.GetProperty("rowNumber").GetInt32())
            .ToArray();
        Assert.Equal([3, 4], rejectedRows);
    }

    [Fact]
    public async Task Importing_the_same_file_again_reports_its_people_as_unchanged()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        await api.CreatePropertyAsync(formId, new { name = "Age", dataType = "number" });
        const string csv = "email,Age\nada@example.com,36\nbob@example.com,41\n";
        Dictionary<string, string> columns = new() { ["age"] = "Age" };
        using HttpResponseMessage first = await api.ImportAsync(formId, csv, columns);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Act
        using HttpResponseMessage second = await api.ImportAsync(
            formId, "email,Age\nada@example.com,36\nbob@example.com,42\n", columns);

        // Assert
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        JsonElement result = await api.ReadAsync(second);
        Assert.Equal(1, result.GetProperty("skippedCount").GetInt32());
        Assert.Equal(1, result.GetProperty("updatedCount").GetInt32());
    }

    [Fact]
    public async Task Importing_without_the_identifier_column_writes_nothing()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        using HttpResponseMessage response = await api.ImportAsync(formId, "name\nAda\n");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement page = await api.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/audience/people", api.CancellationToken);
        Assert.Equal(0, page.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Changing_the_match_key_after_an_import_is_a_conflict()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        using HttpResponseMessage imported = await api.ImportAsync(formId, "email\nada@example.com\n");
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);

        // Act
        using HttpResponseMessage response = await api.PutKeyAsync("external_id");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Parallel_imports_of_the_same_people_on_several_forms_all_succeed()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long[] formIds = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => api.CreateFormAsync()));
        string csv = "email\n" + string.Join("\n", Enumerable.Range(0, 50).Select(i => $"p{i}@example.com"));

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(formIds.Select(formId => api.ImportAsync(formId, csv)));

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        JsonElement[] results = await Task.WhenAll(responses.Select(api.ReadAsync));
        Assert.All(results, result => Assert.Equal(50, result.GetProperty("createdCount").GetInt32()));
        DisposeAll(responses);
    }
}
