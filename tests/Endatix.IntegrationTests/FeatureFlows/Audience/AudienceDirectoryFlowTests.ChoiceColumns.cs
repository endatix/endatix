using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// <c>variable__choice</c> columns read into one multiple-choice Property, with Other: a responses export imports back
/// unchanged, and a cell that is neither ticked nor unticked is refused the same way by the one-request import and a
/// staged import.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private const string BrandsDefinition =
        """{"pages":[{"name":"page1","elements":[{"type":"checkbox","name":"brands","choices":["nike","adidas"],"showOtherItem":true}]}]}""";

    private static readonly object BrandsProperty =
        new { name = "Brands", dataType = "multiple_choice", choicesJson = """["nike","adidas"]""", allowsOther = true };

    private static readonly string[] BrandsHeaders =
        ["email", "brands__nike", "brands__adidas", "brands__other", "brands__other_text"];

    [Fact]
    public async Task A_responses_export_of_a_checkbox_with_other_imports_back_into_a_multiple_choice_property()
    {
        // Arrange
        await using ReportingWebHost host = new(fixture);
        AudienceApi api = await AsAsync(await WorldAsync(host), TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync(definition: BrandsDefinition);
        long brands = await api.CreatePropertyAsync(formId, BrandsProperty);
        await SubmitAsync(api, formId, """{"brands":["nike","other"],"brands-Comment":"Acme"}""");
        await SubmitAsync(api, formId, """{"brands":["adidas"]}""");
        await ExpectOkAsync(api.PostAsync($"/api/forms/{formId}/submissions/backfill", new { }));
        string export = await ExportAsync(api, formId, "csv");

        // Act — the spreadsheet edit: keep the brands columns and add an identifier to each row.
        using HttpResponseMessage imported = await api.ImportAsync(formId, BrandsColumnsWithEmails(export));

        // Assert
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        JsonElement result = await api.ReadAsync(imported);
        Assert.Equal(0, result.GetProperty("rejectedCount").GetInt32());
        Assert.Equal(
            ["""["adidas"]""", """["nike","Acme"]"""],
            (await ValuesOfAsync(api, formId, brands)).Values.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Choice_cells_are_refused_and_stored_alike_by_the_one_request_import_and_a_staged_import()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long inlineForm = await api.CreateFormAsync();
        long inlineBrands = await api.CreatePropertyAsync(inlineForm, BrandsProperty);
        long stagedForm = await api.CreateFormAsync();
        long stagedBrands = await api.CreatePropertyAsync(stagedForm, BrandsProperty);
        List<string[]> rows =
        [
            ["ada@example.com", "1", "no", "", ""],
            ["bob@example.com", "maybe", "0", "0", ""],
            ["cy@example.com", "0", "Y", "x", "Acme"],
            ["dee@example.com", "0", "0", "0", "Acme"],
        ];

        // Act
        using HttpResponseMessage inline = await api.ImportAsync(inlineForm, Csv(BrandsHeaders, rows));
        BulkImport staged = await StageChoiceImportAsync(api, stagedForm, rows);
        JsonElement check = await api.WaitForImportAsync(staged, "checked");
        using HttpResponseMessage confirmed = await api.MoveImportAsync(staged, "writing");
        await api.WaitForImportAsync(staged, "completed");

        // Assert
        Assert.Equal(HttpStatusCode.OK, inline.StatusCode);
        (int, string)[] expected =
        [
            (3, "'brands__nike' must be 1 or 0 (true/false, yes/no, y/n and x also work), not 'maybe'."),
            (5, "'brands__other_text' has text, but 'brands__other' is not ticked."),
        ];
        Assert.Equal(expected, Rejections(await api.ReadAsync(inline)));
        Assert.Equal(expected, Rejections(check));
        Dictionary<string, string> stored = new()
        {
            ["ada@example.com"] = """["nike"]""",
            ["cy@example.com"] = """["adidas","Acme"]""",
        };
        Assert.Equal(stored, await ValuesOfAsync(api, inlineForm, inlineBrands));
        Assert.Equal(stored, await ValuesOfAsync(api, stagedForm, stagedBrands));
    }

    private static async Task SubmitAsync(AudienceApi api, long formId, string jsonData)
    {
        using HttpResponseMessage response = await api.PostAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = true, jsonData });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>The export's <c>brands__*</c> columns, one data row per response, each with a new email.</summary>
    private static string BrandsColumnsWithEmails(string export)
    {
        string[][] lines = [.. export.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r').Split(',').Select(cell => cell.Trim('"')).ToArray())];
        int[] brandColumns = [.. Enumerable.Range(0, lines[0].Length)
            .Where(index => lines[0][index].StartsWith("brands__", StringComparison.Ordinal))];
        Assert.Equal(BrandsHeaders.Skip(1), brandColumns.Select(index => lines[0][index]));
        List<string[]> rows = [.. lines.Skip(1).Select((line, row) =>
            new[] { $"p{row}@example.com" }.Concat(brandColumns.Select(index => line[index])).ToArray())];
        return Csv(BrandsHeaders, rows);
    }

    private static string Csv(string[] headers, IEnumerable<string[]> rows) =>
        string.Join('\n', new[] { headers }.Concat(rows).Select(cells => string.Join(',', cells)));

    private static async Task<BulkImport> StageChoiceImportAsync(AudienceApi api, long formId, List<string[]> rows)
    {
        using HttpResponseMessage started = await api.PostAsync(
            $"/api/forms/{formId}/audience/imports",
            new { fileName = "people.csv", headers = BrandsHeaders, rowCount = rows.Count, identifierColumn = "email" });
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        BulkImport import = new(formId, IdOf(await api.ReadAsync(started), "importId"));
        using HttpResponseMessage sent = await api.SendChunkAsync(import, 0, rows);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        return import;
    }

    private static (int, string)[] Rejections(JsonElement result) =>
        [.. result.GetProperty("rejections").EnumerateArray()
            .Select(rejection => (rejection.GetProperty("rowNumber").GetInt32(), rejection.GetProperty("reason").GetString()!))];

    private static async Task<Dictionary<string, string>> ValuesOfAsync(AudienceApi api, long formId, long propertyId)
    {
        JsonElement page = await api.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/audience/people?pageSize=100", api.CancellationToken);
        return page.GetProperty("items").EnumerateArray()
            .Where(person => person.GetProperty("values").TryGetProperty($"{propertyId}", out _))
            .ToDictionary(
                person => person.GetProperty("identifier").GetString()!,
                person => person.GetProperty("values").GetProperty($"{propertyId}").GetString()!);
    }
}
