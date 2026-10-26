using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task A_staged_import_that_keeps_empty_cells_leaves_the_stored_value()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long city = await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        await api.SeedAsync(formId, "email,City\nada@example.com,Sofia\n", new() { ["city"] = "City" });

        // Act
        JsonElement view = await api.ImportWithAsync(
            formId, ["email", "City"], [["ada@example.com", ""]], new ImportChoices(new() { ["city"] = "City" }));

        // Assert
        Assert.Equal("keep", view.GetProperty("emptyCells").GetString());
        Assert.Equal(1, view.GetProperty("written").GetProperty("skippedCount").GetInt32());
        Assert.Equal("Sofia", (await api.ValuesAsync(formId, "ada@example.com")).GetValueOrDefault(city));
    }

    [Fact]
    public async Task A_staged_import_that_clears_empty_cells_removes_the_stored_value()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long city = await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        await api.SeedAsync(formId, "email,City\nada@example.com,Sofia\nbob@example.com,Varna\n", new() { ["city"] = "City" });

        // Act
        JsonElement view = await api.ImportWithAsync(
            formId,
            ["email", "City"],
            [["ada@example.com", ""], ["bob@example.com", "Ruse"]],
            new ImportChoices(new() { ["city"] = "City" }, EmptyCells: "clear"));

        // Assert
        Assert.Equal("clear", view.GetProperty("emptyCells").GetString());
        Assert.Equal(2, view.GetProperty("written").GetProperty("updatedCount").GetInt32());
        Assert.False((await api.ValuesAsync(formId, "ada@example.com")).ContainsKey(city));
        Assert.Equal("Ruse", (await api.ValuesAsync(formId, "bob@example.com"))[city]);
    }

    [Fact]
    public async Task Clearing_empty_cells_clears_a_choice_Property_whose_row_ticks_no_choice()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long region = await api.CreatePropertyAsync(
            formId, new { name = "Region", dataType = "multiple_choice", choicesJson = """["North","South"]""" });
        string[] headers = ["email", "region__North", "region__South"];
        await api.ImportWithAsync(formId, headers, [["ada@example.com", "x", ""], ["bob@example.com", "", "x"]], new ImportChoices([]));

        // Act
        await api.ImportWithAsync(
            formId, headers, [["ada@example.com", "", ""], ["bob@example.com", "", "x"]], new ImportChoices([], EmptyCells: "clear"));

        // Assert
        Assert.False((await api.ValuesAsync(formId, "ada@example.com")).ContainsKey(region));
        Assert.Equal("""["South"]""", (await api.ValuesAsync(formId, "bob@example.com"))[region]);
    }

    [Fact]
    public async Task Clearing_missing_Properties_clears_only_the_file_people_and_never_the_identifier()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long email = await api.CreatePropertyAsync(formId, new { name = "Email", dataType = "text" });
        long city = await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        long age = await api.CreatePropertyAsync(formId, new { name = "Age", dataType = "number" });
        await api.SeedAsync(
            formId,
            "email,City,Age\nada@example.com,Sofia,36\nbob@example.com,Varna,41\n",
            new() { ["city"] = "City", ["age"] = "Age" });

        // Act
        JsonElement view = await api.ImportWithAsync(
            formId, ["email", "City"], [["ada@example.com", "Sofia"]],
            new ImportChoices(new() { ["city"] = "City" }, MissingProperties: "clear"));

        // Assert
        Assert.Equal("clear", view.GetProperty("missingProperties").GetString());
        Assert.Equal(1, view.GetProperty("written").GetProperty("updatedCount").GetInt32());
        Dictionary<long, string> ada = await api.ValuesAsync(formId, "ada@example.com");
        Assert.False(ada.ContainsKey(age));
        Assert.Equal("Sofia", ada[city]);
        Assert.Equal("ada@example.com", ada[email]);
        Assert.Equal("41", (await api.ValuesAsync(formId, "bob@example.com"))[age]);
    }

    [Fact]
    public async Task Clearing_missing_Properties_keeps_a_choice_Property_whose_choice_columns_are_in_the_file()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long region = await api.CreatePropertyAsync(
            formId, new { name = "Region", dataType = "multiple_choice", choicesJson = """["North","South"]""" });
        string[] headers = ["email", "region__North", "region__South"];
        await api.ImportWithAsync(formId, headers, [["ada@example.com", "x", ""]], new ImportChoices([]));

        // Act
        await api.ImportWithAsync(formId, headers, [["ada@example.com", "", ""]], new ImportChoices([], MissingProperties: "clear"));

        // Assert
        Assert.Equal("""["North"]""", (await api.ValuesAsync(formId, "ada@example.com"))[region]);
    }

    [Fact]
    public async Task A_test_import_that_clears_never_touches_a_person_already_on_the_form()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long city = await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        await api.SeedAsync(formId, "email,City\nada@example.com,Sofia\n", new() { ["city"] = "City" });

        // Act
        JsonElement view = await api.ImportWithAsync(
            formId, ["email", "City"], [["ada@example.com", ""]],
            new ImportChoices(new() { ["city"] = "City" }, EmptyCells: "clear", MissingProperties: "clear", IsTest: true));

        // Assert
        Assert.Equal(1, view.GetProperty("written").GetProperty("skippedCount").GetInt32());
        Assert.Equal("Sofia", (await api.ValuesAsync(formId, "ada@example.com"))[city]);
    }

    [Fact]
    public async Task Clearing_when_nothing_is_left_to_clear_reports_the_people_as_skipped()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        await api.CreatePropertyAsync(formId, new { name = "Age", dataType = "number" });
        ImportChoices clearBoth = new(new() { ["city"] = "City" }, EmptyCells: "clear", MissingProperties: "clear");
        await api.SeedAsync(formId, "email,City\nada@example.com,\n", new() { ["city"] = "City" });

        // Act
        JsonElement view = await api.ImportWithAsync(formId, ["email", "City"], [["ada@example.com", ""]], clearBoth);

        // Assert
        JsonElement written = view.GetProperty("written");
        Assert.Equal(1, written.GetProperty("skippedCount").GetInt32());
        Assert.Equal(0, written.GetProperty("updatedCount").GetInt32());
    }

    [Fact]
    public async Task A_write_cut_off_mid_import_clears_with_the_options_it_started_with_after_it_resumes()
    {
        // Arrange — 600 people with a City; the clearing write's second batch fails as it saves its progress.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        Dictionary<string, string> columns = new() { ["city"] = "City" };
        List<string[]> seeded = [.. Enumerable.Range(1, 600).Select(n => new[] { $"p{n}@example.com", $"City {n}" })];
        await api.ImportWithAsync(formId, ["email", "City"], seeded, new ImportChoices(columns));
        using CommandPause cutOff = world.Services.GetRequiredService<AppCommandGate>().FailAt(
            WriteProgressSave(), new TimeoutException("The connection was lost."), skip: 1);

        // Act
        JsonElement view = await api.ImportWithAsync(
            formId,
            ["email", "City"],
            [.. seeded.Select(row => new[] { row[0], "" })],
            new ImportChoices(columns, EmptyCells: "clear"));

        // Assert
        Assert.True(cutOff.Reached.IsCompleted, "The second write batch was never cut off.");
        Assert.Equal("completed", view.GetProperty("status").GetString());
        Assert.Equal(600, view.GetProperty("written").GetProperty("updatedCount").GetInt32());
        Assert.Equal(0, await api.CountWithValuesAsync(formId));
    }

    [Fact]
    public async Task Starting_an_import_with_an_option_other_than_keep_or_clear_is_refused()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        using HttpResponseMessage started = await api.PostAsync(
            $"/api/forms/{formId}/audience/imports",
            new { fileName = "people.csv", headers = EmailHeader, rowCount = 1, identifierColumn = "email", emptyCells = "wipe" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, started.StatusCode);
        Assert.Contains("keep or clear", (await api.ReadAsync(started)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Every_rejected_row_of_an_import_reads_back_page_by_page_with_its_cells()
    {
        // Arrange — 1,000 rows with no identifier, which the Check rejects, and one row it accepts.
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        List<string[]> rows = [.. Enumerable.Range(1, 1_000).Select(n => new[] { "", $"City {n}" })];
        rows.Add(["ada@example.com", "Sofia"]);
        BulkImport import = await api.StageImportAsync(formId, ["email", "City"], rows);
        Assert.Equal("checked", (await api.WaitForImportAsync(import, "checked")).GetProperty("status").GetString());

        // Act
        List<JsonElement> pages = [];
        for (int page = 1; page <= 3; page++)
        {
            pages.Add(await api.Client.GetFromJsonAsync<JsonElement>(
                $"/api/forms/{formId}/audience/imports/{import.ImportId}/rejections?page={page}&pageSize=400",
                api.CancellationToken));
        }

        // Assert
        Assert.All(pages, page => Assert.Equal(1_000, page.GetProperty("totalRecords").GetInt32()));
        List<JsonElement> items = [.. pages.SelectMany(page => page.GetProperty("items").EnumerateArray())];
        Assert.Equal(Enumerable.Range(2, 1_000), items.Select(item => item.GetProperty("rowNumber").GetInt32()));
        JsonElement last = items[^1];
        Assert.False(string.IsNullOrWhiteSpace(last.GetProperty("reason").GetString()));
        Assert.Equal(["", "City 1000"], last.GetProperty("cells").EnumerateArray().Select(cell => cell.GetString()));
    }

    [Fact]
    public async Task Reading_the_rejections_of_another_form_import_is_not_found()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long otherFormId = await api.CreateFormAsync();
        BulkImport import = await api.StartImportAsync(formId, EmailHeader, 1);

        // Act
        using HttpResponseMessage response = await api.Client.GetAsync(
            $"/api/forms/{otherFormId}/audience/imports/{import.ImportId}/rejections", api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_rejections_with_a_page_over_5000_rows_is_refused()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        BulkImport import = await api.StartImportAsync(formId, EmailHeader, 1);

        // Act
        using HttpResponseMessage response = await api.Client.GetAsync(
            $"/api/forms/{formId}/audience/imports/{import.ImportId}/rejections?pageSize=5001", api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>A staged import's mapping and options, as the Hub's Columns step sends them.</summary>
    private sealed record ImportChoices(
        Dictionary<string, string> PropertyColumns,
        string? EmptyCells = null,
        string? MissingProperties = null,
        bool IsTest = false);

    private sealed partial class AudienceApi
    {
        /// <summary>Imports a CSV in one request and asserts it succeeded.</summary>
        public async Task SeedAsync(long formId, string csv, Dictionary<string, string> propertyColumns)
        {
            using HttpResponseMessage seeded = await ImportAsync(formId, csv, propertyColumns);
            Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        }

        /// <summary>Runs a staged import with <paramref name="choices"/> to its end and returns its view.</summary>
        public async Task<JsonElement> ImportWithAsync(
            long formId,
            string[] headers,
            IReadOnlyList<string[]> rows,
            ImportChoices choices)
        {
            using HttpResponseMessage started = await PostAsync(
                $"/api/forms/{formId}/audience/imports",
                new
                {
                    fileName = "people.csv",
                    headers,
                    rowCount = rows.Count,
                    identifierColumn = headers[0],
                    propertyColumns = choices.PropertyColumns,
                    isTest = choices.IsTest,
                    emptyCells = choices.EmptyCells,
                    missingProperties = choices.MissingProperties,
                });
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
            BulkImport import = new(formId, IdOf(await ReadAsync(started), "importId"));
            for (int index = 0; index * ImportChunkRows < rows.Count; index++)
            {
                using HttpResponseMessage sent = await SendChunkAsync(import, index, rows);
                Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
            }

            Assert.Equal("checked", (await WaitForImportAsync(import, "checked")).GetProperty("status").GetString());
            using HttpResponseMessage confirmed = await MoveImportAsync(import, "writing");
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            return await WaitForImportAsync(import, "completed");
        }

        /// <summary>The form's participants with any stored value.</summary>
        public async Task<int> CountWithValuesAsync(long formId)
        {
            JsonElement page = await client.GetFromJsonAsync<JsonElement>(
                $"/api/forms/{formId}/audience/people?pageSize=5000", cancellationToken);
            return page.GetProperty("items").EnumerateArray()
                .Count(item => item.GetProperty("values").EnumerateObject().Any());
        }

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
