using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

public sealed partial class ScenarioCsvExportFlowTests
{
    [Fact]
    public async Task Duplicate_matrix_codes_refuse_the_new_definition_but_prior_submissions_still_flatten()
    {
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        ScenarioApi api = new(client, ct);
        var id = await api.CreateFormAsync(Matrix("10,20"));
        var compile = $"/api/forms/{id}/reporting/compile-schema";
        var backfill = $"/api/forms/{id}/submissions/backfill";
        await api.SubmitCompleteAsync(id, """{"m":{"r":10}}""");
        await api.PostOkAsync(compile, new { });
        await api.PostOkAsync(backfill, new { force = true });
        var good = await api.ExportCsvAsync(id);
        await api.PublishDefinitionAsync(id, Matrix("1,1.0"));
        await api.SubmitCompleteAsync(id, """{"m":{"r":1}}""");

        foreach (var replace in new[] { false, true })
        {
            using var refusal = await client.PostAsJsonAsync(compile, new { replace }, ct);
            refusal.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var problem = await refusal.Content.ReadFromJsonAsync<JsonElement>(ct);
            problem.GetProperty("previousSchemaRetained").GetBoolean().Should().BeTrue();
            (await api.ExportCsvAsync(id)).Should().Be(good);
        }

        using var run = await client.PostAsJsonAsync(backfill, new { force = true }, ct);
        run.EnsureSuccessStatusCode();
        var counts = await run.Content.ReadFromJsonAsync<JsonElement>(ct);
        counts.GetProperty("processed").GetInt32().Should().Be(1);
        counts.GetProperty("failed").GetInt32().Should().Be(1);
        (await api.ExportCsvAsync(id)).Should().Be(good);
    }

    private static string Matrix(string columns) =>
        $$"""{"elements":[{"name":"m","type":"matrix","rows":["r"],"columns":[{{columns}}]}]}""";

    [Fact]
    public async Task Rejected_in_place_matrix_edit_cannot_reflatten_with_the_old_mapping()
    {
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        ScenarioApi api = new(client, ct);
        var id = await api.CreateFormAsync(Matrix("10,20"));
        var backfill = $"/api/forms/{id}/submissions/backfill";
        await api.SubmitCompleteAsync(id, """{"m":{"r":10}}""");
        await api.PostOkAsync($"/api/forms/{id}/reporting/compile-schema", new { });
        await api.PostOkAsync(backfill, new { force = true });
        var saved = await api.ExportCsvAsync(id);
        using var edit = await client.PutAsJsonAsync($"/api/forms/{id}/definition", new { isDraft = false, jsonData = Matrix("1,1.0") }, ct);
        edit.EnsureSuccessStatusCode();
        await api.SubmitCompleteAsync(id, """{"m":{"r":1}}""");

        using var run = await client.PostAsJsonAsync(backfill, new { force = true }, ct);
        run.EnsureSuccessStatusCode();
        var counts = await run.Content.ReadFromJsonAsync<JsonElement>(ct);
        counts.GetProperty("failed").GetInt32().Should().Be(2);
        (await api.ExportCsvAsync(id)).Should().Be(saved);
    }
}
