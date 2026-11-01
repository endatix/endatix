using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Answer filters on <c>GET forms/{formId}/reporting/submissions</c> over HTTP on <see cref="ReportingWebHost"/>, with
/// search copies written by a real compile and backfill: the <c>answer</c> parameter, <c>answerFilterCoverage</c> and
/// the problem responses the Hub reads (#1091).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class SubmissionGridAnswerFiltersFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const long MissingFormId = 9_999_999_999L;
    private const string KeyWithColon = "ref:code";
    private static readonly string[] VolatileMembers = ["traceId"];

    private const string Definition = """
        {
          "pages": [
            {
              "name": "visit",
              "elements": [
                { "type": "text", "name": "company" },
                { "type": "comment", "name": "notes" },
                { "type": "dropdown", "name": "size", "choices": [
                  { "value": "opt-m", "text": "Medium" }, { "value": "opt-l", "text": "Large" } ] },
                { "type": "radiogroup", "name": "color", "choices": [
                  { "value": "c-red", "text": "Red" }, { "value": "c-blue", "text": "Blue" } ] },
                { "type": "text", "name": "visit", "inputType": "date" },
                { "type": "text", "name": "ref:code" }
              ]
            }
          ]
        }
        """;

    private const string AcmeAnswers =
        """{"company":"Acme Corp","notes":"Call BACK","size":"opt-m","color":"c-red","visit":"2026-03-01","ref:code":"X-1"}""";

    private const string GlobexAnswers =
        """{"company":"Globex","size":"opt-l","color":"c-blue","visit":"2026-04-02","ref:code":"Y-2"}""";

    [Fact]
    public async Task Contains_matches_text_comment_and_the_stored_choice_value()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateFormAsync(client, compile: true, ct);
        var acme = await SubmitAsync(client, formId, AcmeAnswers, isComplete: true, ct);
        var globex = await SubmitAsync(client, formId, GlobexAnswers, isComplete: true, ct);
        await BackfillAsync(client, formId, ct);
        Dictionary<string, string> filters = new()
        {
            ["company"] = "contains:company:ACME",
            ["notes"] = "contains:notes:back",
            ["dropdown value"] = "contains:size:opt-m",
            ["dropdown text"] = "contains:size:Medium",
            ["radio value"] = "contains:color:c-blue",
            ["key with a colon"] = $"contains:{Uri.EscapeDataString(KeyWithColon)}:y-2",
        };

        // Act
        Dictionary<string, string> matches = [];
        foreach (var (name, filter) in filters)
        {
            using var page = await GetOkAsync(client, Rows(formId, filter), ct);
            matches[name] = Ids(page.RootElement);
        }

        // Assert
        matches.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["company"] = acme,
            ["notes"] = acme,
            ["dropdown value"] = acme,
            ["dropdown text"] = "",
            ["radio value"] = globex,
            ["key with a colon"] = globex,
        });
    }

    [Fact]
    public async Task Repeated_answer_parameters_combine_with_and()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateFormAsync(client, compile: true, ct);
        var acme = await SubmitAsync(client, formId, AcmeAnswers, isComplete: true, ct);
        await SubmitAsync(client, formId, GlobexAnswers, isComplete: true, ct);
        await SubmitAsync(client, formId, AcmeAnswers.Replace("c-red", "c-blue", StringComparison.Ordinal), isComplete: true, ct);
        await BackfillAsync(client, formId, ct);

        // Act
        using var page = await GetOkAsync(
            client, Rows(formId, "contains:color:c-red", "from:visit:2026-03-01", "to:visit:2026-03-31"), ct);

        // Assert
        Ids(page.RootElement).Should().Be(acme);
        page.RootElement.GetProperty("totalRecords").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Coverage_counts_the_submissions_that_could_not_be_checked()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateFormAsync(client, compile: true, ct);
        var acme = await SubmitAsync(client, formId, AcmeAnswers, isComplete: true, ct);
        var changed = await SubmitAsync(client, formId, GlobexAnswers, isComplete: true, ct);
        await SubmitAsync(client, formId, AcmeAnswers, isComplete: false, ct);
        await BackfillAsync(client, formId, ct);
        await ChangeAnswersAsync(client, formId, changed, AcmeAnswers, ct);

        // Act
        using var filtered = await GetOkAsync(client, Rows(formId, "contains:company:acme"), ct);
        using var unfiltered = await GetOkAsync(client, Rows(formId), ct);

        // Assert
        Ids(filtered.RootElement).Should().Be(acme);
        filtered.RootElement.GetProperty("answerFilterCoverage").GetRawText().Replace(" ", "", StringComparison.Ordinal)
            .Should().Be("""{"uncheckedCompleted":1,"uncheckedInProgress":1}""");
        unfiltered.RootElement.TryGetProperty("answerFilterCoverage", out _).Should().BeFalse();
    }

    [Fact]
    public async Task With_an_answer_filter_every_row_matches_the_rows_follow_the_submissions_list()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateFormAsync(client, compile: true, ct);
        var approved = await SubmitAsync(client, formId, AcmeAnswers, isComplete: true, ct);
        await SetStatusAsync(client, formId, approved, "approved", ct);
        var edited = await SubmitAsync(client, formId, AcmeAnswers, isComplete: true, ct, currentPage: 2);
        await ChangeAnswersAsync(client, formId, edited, AcmeAnswers.Replace("Acme Corp", "Acme Ltd", StringComparison.Ordinal), ct);
        await SubmitAsync(client, formId, AcmeAnswers, isComplete: false, ct, currentPage: 1);
        await SubmitAsync(client, formId, AcmeAnswers, isComplete: true, ct);
        await BackfillAsync(client, formId, ct);
        await BackfillAsync(client, formId, ct, completionScope: "incomplete");
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1).ToString("yyyy-MM-dd");
        var tomorrow = today.AddDays(1).ToString("yyyy-MM-dd");
        string[] queries =
        [
            "",
            $"filter={Uri.EscapeDataString("status:approved")}",
            $"filter={Uri.EscapeDataString("status!:approved")}",
            $"filter={Uri.EscapeDataString("collectionStatus:complete")}",
            $"filter={Uri.EscapeDataString("collectionStatus!:complete")}",
            $"filter={Uri.EscapeDataString("isComplete:false")}",
            $"filter={Uri.EscapeDataString("currentPage:2")}",
            $"createdFrom={yesterday}&createdTo={tomorrow}",
            $"createdTo={yesterday}",
            $"modifiedFrom={yesterday}&modifiedTo={tomorrow}",
            $"startedFrom={yesterday}",
            $"completedFrom={yesterday}&completedTo={tomorrow}",
            "sortBy=ModifiedAt&sortDir=Desc&pageSize=2",
            "sortBy=StartedAt&sortDir=Asc&page=2&pageSize=2",
            "sortBy=CompletedAt&sortDir=Desc&pageSize=3",
            "sortBy=CreatedAt&sortDir=Asc&page=2&pageSize=3",
            "sortBy=Id&sortDir=Desc&pageSize=2",
        ];

        // Act
        Dictionary<string, string> list = [];
        Dictionary<string, string> grid = [];
        foreach (var query in queries)
        {
            using var listed = await GetOkAsync(client, $"/api/forms/{formId}/submissions?{query}", ct);
            using var filtered = await GetOkAsync(client, $"{Rows(formId, "contains:company:acme")}&{query}", ct);
            list[query] = Window(listed.RootElement);
            grid[query] = Window(filtered.RootElement);
        }

        using var everyRow = await GetOkAsync(client, Rows(formId, "contains:company:acme"), ct);

        // Assert
        everyRow.RootElement.GetProperty("answerFilterCoverage").GetRawText().Replace(" ", "", StringComparison.Ordinal)
            .Should().Be("""{"uncheckedCompleted":0,"uncheckedInProgress":0}""");
        grid.Should().BeEquivalentTo(list);
    }

    [Theory]
    [InlineData("contains:missing:x", "Column 'missing' cannot be filtered on this form.")]
    [InlineData("from:company:2026-01-01", "Column 'company' is a text column and does not support 'from'.")]
    [InlineData("to:visit:2026-02-30",
        "Each answer filter must be contains:{key}:{text} (1 to 200 characters), from:{key}:{yyyy-MM-dd} or to:{key}:{yyyy-MM-dd}.")]
    public async Task An_invalid_answer_filter_is_a_bad_request_problem_on_the_answer_field(string filter, string message)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateFormAsync(client, compile: true, ct);

        // Act
        using var response = await client.GetAsync(Rows(formId, filter), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        problem.RootElement.GetProperty("fields").GetProperty("answer")[0].GetString().Should().Be(message);
    }

    [Fact]
    public async Task A_form_without_a_prepared_schema_is_a_conflict_only_with_answer_filters()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateFormAsync(client, compile: false, ct);
        await SubmitAsync(client, formId, AcmeAnswers, isComplete: true, ct);

        // Act
        using var filtered = await client.GetAsync(Rows(formId, "contains:company:acme"), ct);
        using var unfiltered = await client.GetAsync(Rows(formId), ct);

        // Assert
        filtered.StatusCode.Should().Be(HttpStatusCode.Conflict);
        filtered.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var expected = ProblemDetailsJson.Shape(
            $$"""
            {
              "type": "https://www.rfc-editor.org/rfc/rfc9110.html#name-409-conflict",
              "title": "There was a conflict",
              "status": 409,
              "detail": "The form has no prepared reporting schema, so its answers cannot be filtered.",
              "instance": "/api/forms/{{formId}}/reporting/submissions",
              "traceId": "<string>",
              "errorCode": "reporting_not_prepared"
            }
            """,
            VolatileMembers);
        (await ProblemDetailsJson.ReadShapeAsync(filtered, ct, VolatileMembers)).Should().Be(expected);
        unfiltered.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Answer_filters_on_an_unknown_form_are_a_not_found_problem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);

        // Act
        using var response = await client.GetAsync(Rows(MissingFormId.ToString(), "contains:company:acme"), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        problem.RootElement.GetProperty("detail").GetString().Should().Be("Form not found.");
    }

    private static string Rows(string formId, params string[] answers) =>
        $"/api/forms/{formId}/reporting/submissions" +
        (answers.Length == 0 ? "" : "?" + string.Join('&', answers.Select(answer => $"answer={Uri.EscapeDataString(answer)}")));

    private static string Window(JsonElement page) =>
        $"{page.GetProperty("page").GetInt32()}/{page.GetProperty("pageSize").GetInt32()}/" +
        $"{page.GetProperty("totalRecords").GetInt32()}:{Ids(page)}";

    private static string Ids(JsonElement page) =>
        string.Join(',', page.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("id").GetString()));

    private static async Task<HttpClient> TenantAdminAsync(ReportingWebHost host, CancellationToken ct)
    {
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
    }

    private static async Task<string> CreateFormAsync(HttpClient client, bool compile, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/forms",
            new { name = $"grid-answers-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = Definition },
            ct);
        response.EnsureSuccessStatusCode();
        using var form = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var formId = form.RootElement.GetProperty("id").GetString()!;
        if (compile)
        {
            using var compiled = await client.PostAsJsonAsync($"/api/forms/{formId}/reporting/compile-schema", new { }, ct);
            compiled.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        return formId;
    }

    private static async Task<string> SubmitAsync(
        HttpClient client,
        string formId,
        string jsonData,
        bool isComplete,
        CancellationToken ct,
        int? currentPage = null)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions", new { isComplete, jsonData, currentPage }, ct);
        response.EnsureSuccessStatusCode();
        using var submission = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return submission.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task BackfillAsync(
        HttpClient client,
        string formId,
        CancellationToken ct,
        string completionScope = "completed")
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/backfill", new { completionScope }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task SetStatusAsync(
        HttpClient client,
        string formId,
        string submissionId,
        string status,
        CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/{submissionId}/status", new { status }, ct);
        response.EnsureSuccessStatusCode();
    }

    private static async Task ChangeAnswersAsync(
        HttpClient client,
        string formId,
        string submissionId,
        string jsonData,
        CancellationToken ct)
    {
        using var response = await client.PatchAsJsonAsync(
            $"/api/forms/{formId}/submissions/{submissionId}", new { jsonData }, ct);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonDocument> GetOkAsync(HttpClient client, string uri, CancellationToken ct)
    {
        using var response = await client.GetAsync(uri, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
}
