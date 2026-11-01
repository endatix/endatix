using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// <c>GET forms/{formId}/reporting/submissions</c> over HTTP on <see cref="ReportingWebHost"/>, with rows prepared by a
/// real backfill: the JSON the submissions grid reads, parity with the submissions list, and tenant isolation (#1255).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class SubmissionGridRowsFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const long MissingFormId = 9_999_999_999L;
    private static readonly string[] VolatileMembers = ["traceId"];

    private const string Definition = """
        {
          "pages": [
            {
              "name": "visit",
              "elements": [
                { "type": "text", "name": "city" },
                { "type": "comment", "name": "notes" }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task Rows_carry_their_state_and_only_the_requested_answers()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateCompiledFormAsync(client, ct);
        var prepared = await SubmitAsync(client, formId, """{"city":"Sofia","notes":"first"}""", isComplete: true, ct);
        var changed = await SubmitAsync(client, formId, """{"city":"Varna","notes":"second"}""", isComplete: true, ct);
        var inProgress = await SubmitAsync(client, formId, """{"city":"Burgas"}""", isComplete: false, ct);
        await BackfillAsync(client, formId, ct);
        await ChangeAnswersAsync(client, formId, changed, """{"city":"Ruse","notes":"second"}""", ct);

        // Act
        using var page = await GetOkAsync(client, $"/api/forms/{formId}/reporting/submissions?columns=city", ct);

        // Assert
        var rows = page.RootElement.GetProperty("items").EnumerateArray()
            .ToDictionary(row => row.GetProperty("id").GetString()!);
        rows.Should().HaveCount(3);
        Summary(rows[prepared]).Should().Be("prepared|prepared|{\"city\":\"Sofia\"}");
        Summary(rows[changed]).Should().Be("stale|raw|{\"city\":\"Ruse\"}");
        Summary(rows[inProgress]).Should().Be("none|raw|{\"city\":\"Burgas\"}");
    }

    [Fact]
    public async Task Rows_match_the_submissions_list_for_the_same_filter_sort_and_page()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateCompiledFormAsync(client, ct);
        for (var i = 0; i < 4; i++)
        {
            await SubmitAsync(client, formId, $$"""{"city":"city-{{i}}"}""", isComplete: true, ct);
        }

        await SubmitAsync(client, formId, """{"city":"draft"}""", isComplete: false, ct);
        var query = $"filter={Uri.EscapeDataString("isComplete:true")}&sortBy=CreatedAt&sortDir=Asc&page=2&pageSize=2";

        // Act
        using var list = await GetOkAsync(client, $"/api/forms/{formId}/submissions?{query}", ct);
        using var grid = await GetOkAsync(client, $"/api/forms/{formId}/reporting/submissions?{query}", ct);

        // Assert
        Window(grid.RootElement).Should().Be(Window(list.RootElement));
        Window(grid.RootElement).Should().StartWith("2/2/4:");
    }

    [Fact]
    public async Task Another_tenants_form_is_a_not_found_problem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword }, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: ct);
        var formId = await CreateCompiledFormAsync(owner, ct);
        await SubmitAsync(owner, formId, """{"city":"Pleven"}""", isComplete: true, ct);
        using var caller = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 0, cancellationToken: ct);

        // Act
        using var response = await caller.GetAsync($"/api/forms/{formId}/reporting/submissions?columns=city", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().NotContain("Pleven");
    }

    [Fact]
    public async Task Unknown_form_is_a_not_found_problem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);

        // Act
        using var response = await client.GetAsync($"/api/forms/{MissingFormId}/reporting/submissions", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var expected = ProblemDetailsJson.Shape(
            $$"""
            {
              "type": "https://www.rfc-editor.org/rfc/rfc9110.html#name-404-not-found",
              "title": "Resource not found",
              "status": 404,
              "detail": "Form not found.",
              "instance": "/api/forms/{{MissingFormId}}/reporting/submissions",
              "traceId": "<string>"
            }
            """,
            VolatileMembers);
        (await ProblemDetailsJson.ReadShapeAsync(response, ct, VolatileMembers)).Should().Be(expected);
    }

    [Fact]
    public async Task Too_many_columns_is_a_bad_request_problem_on_the_columns_field()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateCompiledFormAsync(client, ct);
        var columns = string.Join(',', Enumerable.Range(0, 501).Select(i => $"c{i}"));

        // Act
        using var response = await client.GetAsync(
            $"/api/forms/{formId}/reporting/submissions?columns={columns}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        problem.RootElement.GetProperty("fields").GetProperty("columns")[0].GetString()
            .Should().Be("At most 500 columns can be requested.");
    }

    private static string Summary(JsonElement row) =>
        $"{row.GetProperty("rowState").GetString()}|{row.GetProperty("answersSource").GetString()}|" +
        row.GetProperty("answers").GetRawText().Replace(" ", "", StringComparison.Ordinal);

    private static string Window(JsonElement page) =>
        $"{page.GetProperty("page").GetInt32()}/{page.GetProperty("pageSize").GetInt32()}/" +
        $"{page.GetProperty("totalRecords").GetInt32()}:" +
        string.Join(',', page.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("id").GetString()));

    private static async Task<HttpClient> TenantAdminAsync(ReportingWebHost host, CancellationToken ct)
    {
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
    }

    private static async Task<string> CreateCompiledFormAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/forms",
            new { name = $"grid-rows-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = Definition },
            ct);
        response.EnsureSuccessStatusCode();
        using var form = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var formId = form.RootElement.GetProperty("id").GetString()!;
        using var compile = await client.PostAsJsonAsync($"/api/forms/{formId}/reporting/compile-schema", new { }, ct);
        compile.StatusCode.Should().Be(HttpStatusCode.OK);
        return formId;
    }

    private static async Task<string> SubmitAsync(
        HttpClient client,
        string formId,
        string jsonData,
        bool isComplete,
        CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions", new { isComplete, jsonData }, ct);
        response.EnsureSuccessStatusCode();
        using var submission = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return submission.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task BackfillAsync(HttpClient client, string formId, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync($"/api/forms/{formId}/submissions/backfill", new { }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
