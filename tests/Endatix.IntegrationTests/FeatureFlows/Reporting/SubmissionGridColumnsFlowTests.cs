using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// <c>GET forms/{formId}/reporting/columns</c> over HTTP on <see cref="ReportingWebHost"/>: the JSON the submissions
/// grid reads, the locale query, and the answers for a form with no schema and for an unknown form (#1254).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class SubmissionGridColumnsFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const long MissingFormId = 9_999_999_999L;
    private static readonly string[] VolatileMembers = ["traceId"];

    private const string Definition = """
        {
          "pages": [
            {
              "name": "contact",
              "title": { "default": "Contact details", "es": "Datos de contacto" },
              "elements": [
                { "type": "text", "name": "visitDate", "inputType": "date", "title": { "default": "Visit date", "es": "Fecha de visita" } },
                {
                  "type": "panel",
                  "name": "preferences",
                  "title": "Preferences",
                  "elements": [
                    { "type": "radiogroup", "name": "channel", "title": "Channel", "choices": ["email", "phone"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task Columns_of_a_compiled_form_carry_filter_type_page_and_panel()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateCompiledFormAsync(client, ct);

        // Act
        using var columns = await GetColumnsAsync(client, $"/api/forms/{formId}/reporting/columns", ct);

        // Assert
        var root = columns.RootElement;
        root.GetProperty("schemaReady").GetBoolean().Should().BeTrue();
        var visitDate = Column(root, "visitDate");
        visitDate.GetProperty("title").GetString().Should().Be("Visit date");
        visitDate.GetProperty("filterType").GetString().Should().Be("date");
        visitDate.GetProperty("pageName").GetString().Should().Be("contact");
        visitDate.GetProperty("pageTitle").GetString().Should().Be("Contact details");
        var channel = Column(root, "channel");
        channel.GetProperty("filterType").GetString().Should().Be("text");
        channel.GetProperty("panelTitle").GetString().Should().Be("Preferences");
    }

    [Fact]
    public async Task Locale_query_returns_titles_in_that_locale()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateCompiledFormAsync(client, ct);

        // Act
        using var columns = await GetColumnsAsync(client, $"/api/forms/{formId}/reporting/columns?locale=es", ct);

        // Assert
        var visitDate = Column(columns.RootElement, "visitDate");
        visitDate.GetProperty("title").GetString().Should().Be("Fecha de visita");
        visitDate.GetProperty("pageTitle").GetString().Should().Be("Datos de contacto");
    }

    [Fact]
    public async Task Form_without_a_schema_is_not_ready_and_has_no_columns()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);
        var formId = await CreateFormAsync(client, ct);

        // Act
        using var columns = await GetColumnsAsync(client, $"/api/forms/{formId}/reporting/columns", ct);

        // Assert
        columns.RootElement.GetProperty("schemaReady").GetBoolean().Should().BeFalse();
        columns.RootElement.GetProperty("columns").GetArrayLength().Should().Be(0);
        columns.RootElement.GetProperty("readiness").GetProperty("prepared").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Unknown_form_is_a_not_found_problem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);

        // Act
        using var response = await client.GetAsync($"/api/forms/{MissingFormId}/reporting/columns", ct);

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
              "instance": "/api/forms/{{MissingFormId}}/reporting/columns",
              "traceId": "<string>"
            }
            """,
            VolatileMembers);
        (await ProblemDetailsJson.ReadShapeAsync(response, ct, VolatileMembers)).Should().Be(expected);
    }

    private static async Task<HttpClient> TenantAdminAsync(ReportingWebHost host, CancellationToken ct)
    {
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
    }

    private static async Task<string> CreateCompiledFormAsync(HttpClient client, CancellationToken ct)
    {
        var formId = await CreateFormAsync(client, ct);
        using var compile = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/reporting/compile-schema", new { }, ct);
        compile.StatusCode.Should().Be(HttpStatusCode.OK);
        return formId;
    }

    private static async Task<string> CreateFormAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/forms",
            new { name = $"grid-columns-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = Definition },
            ct);
        response.EnsureSuccessStatusCode();
        using var form = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return form.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<JsonDocument> GetColumnsAsync(HttpClient client, string uri, CancellationToken ct)
    {
        using var response = await client.GetAsync(uri, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private static JsonElement Column(JsonElement root, string key) =>
        root.GetProperty("columns").EnumerateArray().Single(column => column.GetProperty("key").GetString() == key);
}
