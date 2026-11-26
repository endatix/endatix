using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

public sealed partial class ScenarioCsvExportFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Default_merge_of_a_legacy_matrix_requires_the_unmodified_historical_source(bool editInPlace)
    {
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        ScenarioApi api = new(client, ct);
        var form = await api.CreateFormAsync(LegacyFixture("definition"));
        var compile = $"/api/forms/{form}/reporting/compile-schema";
        await api.SubmitCompleteAsync(form, """{"rating":{"a":2}}""");
        await api.PostOkAsync(compile, new { });
        await api.PostOkAsync($"/api/forms/{form}/submissions/backfill", new { force = true });
        await using var db = ReportingTestSchema.CreateContext(host.Database.ConnectionString, host.Database.Provider, IntegrationTenantContext.Bypass);
        var saved = db.FormSchemas.AsNoTracking().Where(s => s.FormId == long.Parse(form));
        await saved.ExecuteUpdateAsync(s => s.SetProperty(v => v.Codebook, LegacyFixture("codebook"))
            .SetProperty(v => v.FlatteningMap, LegacyFixture("map")), ct);
        var before = await saved.Select(s => s.Codebook).SingleAsync(ct);
        var next = NextLegacyDefinition(editInPlace);
        if (editInPlace)
        {
            using var edit = await client.PutAsJsonAsync($"/api/forms/{form}/definition", new { isDraft = false, jsonData = next }, ct);
            edit.EnsureSuccessStatusCode();
        }
        else
        {
            await api.PublishDefinitionAsync(form, next);
        }

        using var result = await client.PostAsJsonAsync(compile, new { }, ct);
        result.StatusCode.Should().Be(editInPlace ? HttpStatusCode.Conflict : HttpStatusCode.OK);
        var after = await saved.Select(s => s.Codebook).SingleAsync(ct);
        if (editInPlace)
        {
            after.Should().Be(before);
            return;
        }

        AssertRepairedLegacyBook(after);
        Lines(await api.ExportCsvAsync(form)).Should().HaveCount(2);
    }

    private static string NextLegacyDefinition(bool editInPlace)
    {
        var next = JsonNode.Parse(LegacyFixture("definition"))!;
        var elements = next["pages"]![0]!["elements"]!.AsArray();
        elements.Add(JsonNode.Parse("""{"type":"text","name":"addedField"}"""));
        if (editInPlace)
        {
            elements[0]!["columns"]![0]!["value"] = 20;
        }

        return next.ToJsonString();
    }

    private static void AssertRepairedLegacyBook(string after)
    {
        using var book = JsonDocument.Parse(after);
        var questions = book.RootElement.GetProperty("questions");
        questions.TryGetProperty("addedField", out _).Should().BeTrue();
        var choices = questions.GetProperty("rating").GetProperty("columns").EnumerateArray().ToList();
        choices.Select(c => c.GetProperty("id").GetInt32()).Should().Equal(1, 2, 3, 4);
        choices.Select(c => c.GetProperty("value").GetString()).Should().Equal("1", "2", "3", "4");
    }

    private static string LegacyFixture(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyMatrix", file + ".json"));
}
