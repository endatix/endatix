using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Each export golden scenario with a submission (<c>Fixtures/Scenarios/</c> in the Reporting tests) run end to end over
/// HTTP on <see cref="ReportingWebHost"/>: create the form, submit the response as complete, compile, backfill and export
/// with the tenant's CSV format. The answer columns must match the scenario's <c>expected-csv.csv</c>, which the golden
/// tests build in memory, so the goldens are proven against the running pipeline.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class ScenarioCsvExportFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SEED_PASSWORD = "Password123!";

    // FormId, Id, IsComplete, CollectionStatus, CreatedAt, ModifiedAt, StartedAt, CompletedAt, DurationSeconds,
    // SubmitterId and SubmitterDisplayId: values the run sets, none of which holds a comma.
    private const int SYSTEM_COLUMNS = 11;

    private static string ScenariosRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Scenarios");

    // A scenario with earlier form versions (versions.json) merges them in memory; over HTTP a compile with no real
    // submissions replaces the schema, so the earlier versions' columns would not be there to compare.
    public static TheoryData<string> ScenariosWithASubmission() =>
        new(Directory.EnumerateDirectories(ScenariosRoot)
            .Where(folder => File.Exists(Path.Combine(folder, "submission.json")) &&
                             !File.Exists(Path.Combine(folder, "versions.json")))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(ScenariosWithASubmission))]
    public async Task Scenario_exported_over_http_matches_its_csv_golden(string scenario)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var folder = Path.Combine(ScenariosRoot, scenario);
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        ScenarioApi api = new(client, ct);
        var formId = await api.CreateFormAsync(File.ReadAllText(Path.Combine(folder, "definition.json")));
        await api.SubmitCompleteAsync(formId, File.ReadAllText(Path.Combine(folder, "submission.json")));
        await api.PostOkAsync($"/api/forms/{formId}/reporting/compile-schema", new { });
        await api.PostOkAsync($"/api/forms/{formId}/submissions/backfill", new { force = true });

        // Act
        var csv = await api.ExportCsvAsync(formId);

        // Assert
        var expected = Lines(File.ReadAllText(Path.Combine(folder, "expected-csv.csv")));
        var actual = Lines(csv);
        actual.Should().HaveCount(2, because: $"scenario '{scenario}' exports a header and one row");
        actual[0].Should().Be(expected[0], because: $"scenario '{scenario}' should export the golden's columns");
        Answers(actual[1]).Should().Be(
            Answers(expected[1]),
            because: $"scenario '{scenario}' should export the golden's answers over HTTP");
    }

    // The issue's repro (#1758): a completed response saved before a checkbox was added must not export 0 for it.
    [Fact]
    public async Task Checkbox_added_after_a_response_exports_empty_for_that_response()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        ScenarioApi api = new(client, ct);
        const string before = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"customer"}]}]}""";
        const string after =
            """{"pages":[{"name":"p1","elements":[{"type":"text","name":"customer"},{"type":"checkbox","name":"perks","choices":["1","2"]}]}]}""";
        var formId = await api.CreateFormAsync(before);
        await api.SubmitCompleteAsync(formId, """{"customer":"Sam"}""");
        await api.PostOkAsync($"/api/forms/{formId}/reporting/compile-schema", new { });
        await api.PublishDefinitionAsync(formId, after);
        await api.SubmitCompleteAsync(formId, """{"customer":"Kim"}""");
        await api.PostOkAsync($"/api/forms/{formId}/reporting/compile-schema", new { });
        await api.PostOkAsync($"/api/forms/{formId}/submissions/backfill", new { force = true });

        // Act
        var csv = await api.ExportCsvAsync(formId);

        // Assert
        var lines = Lines(csv);
        lines[0].Should().EndWith(",customer,perks__1,perks__2");
        lines.Skip(1).Select(Answers).Should().BeEquivalentTo("Sam,,", "Kim,0,0");
    }

    private static string[] Lines(string csv) =>
        csv.TrimStart('\uFEFF').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Published_comment_conflict_rejects_compile_and_keeps_the_previous_download(bool replace)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        ScenarioApi api = new(client, ct);
        var formId = await api.CreateFormAsync(Checkbox("""{"value":"a","showCommentArea":true}"""));
        var compile = $"/api/forms/{formId}/reporting/compile-schema";
        await api.SubmitCompleteAsync(formId, """{"q":[{"value":"a","comment":"Earlier note"}]}""");
        await api.PostOkAsync(compile, new { });
        await api.PostOkAsync($"/api/forms/{formId}/submissions/backfill", new { force = true });
        var original = await api.ExportCsvAsync(formId);
        await api.PublishDefinitionAsync(formId, Checkbox("\"a--comment\""));

        // Act
        using var rejected = await client.PostAsJsonAsync(compile, new { replace }, ct);

        // Assert
        rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var detail = await rejected.Content.ReadFromJsonAsync<JsonElement>(ct);
        detail.GetProperty("errorCode").GetString().Should().Be("ReportingSchemaAliasConflict");
        detail.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        detail.GetProperty("previousSchemaRetained").GetBoolean().Should().BeTrue();
        detail.GetProperty("previousExportAvailable").GetBoolean().Should().BeTrue();
        original.Should().Contain("Earlier note");
        (await api.ExportCsvAsync(formId)).Should().Be(original);
    }

    private static string Checkbox(string choices) =>
        """{"pages":[{"elements":[{"name":"q","type":"checkbox","choices":[""" + choices + "]}]}]}";

    private static string Answers(string row) => row.Split(',', SYSTEM_COLUMNS + 1)[SYSTEM_COLUMNS];

    // The admin's client and the test's token, so each call names only what it sends.
    private sealed class ScenarioApi(HttpClient client, CancellationToken ct)
    {
        public async Task<string> CreateFormAsync(string definition)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/forms",
                new { name = $"scenario-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = definition },
                ct);
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
            using var form = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return form.RootElement.GetProperty("id").GetString()!;
        }

        public Task SubmitCompleteAsync(string formId, string jsonData) =>
            PostAsync($"/api/forms/{formId}/submissions", new { isComplete = true, jsonData }, HttpStatusCode.Created);

        public Task PostOkAsync(string uri, object body) => PostAsync(uri, body, HttpStatusCode.OK);

        public Task PublishDefinitionAsync(string formId, string jsonData) =>
            PostAsync($"/api/forms/{formId}/definitions", new { isDraft = false, jsonData }, HttpStatusCode.Created);

        public async Task<string> ExportCsvAsync(string formId)
        {
            var formats = await client.GetFromJsonAsync<JsonElement>("/api/settings/export-formats", ct);
            var csvFormat = formats.EnumerateArray().Single(format => format.GetProperty("wireKey").GetString() == "csv");
            using var response = await client.PostAsJsonAsync(
                $"/api/forms/{formId}/submissions/export",
                new { exportFormatId = csvFormat.GetProperty("id").GetString() },
                ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
            return await response.Content.ReadAsStringAsync(ct);
        }

        private async Task PostAsync(string uri, object body, HttpStatusCode expected)
        {
            using var response = await client.PostAsJsonAsync(uri, body, ct);
            response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync(ct));
        }
    }
}
