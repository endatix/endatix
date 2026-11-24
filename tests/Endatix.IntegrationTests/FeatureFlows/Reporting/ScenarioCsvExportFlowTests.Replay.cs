using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class ScenarioCsvExportFlowTests
{
    [Fact]
    public async Task Completion_replay_keeps_both_prepared_responses_in_each_csv_download()
    {
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        ScenarioApi api = new(client, ct);
        using var format = await client.PostAsJsonAsync("/api/settings/export-formats",
            new { name = "Shoji CSV", exportTarget = "Submissions", deliveryFormat = "Csv", profile = "Shoji" }, ct);
        format.EnsureSuccessStatusCode();
        var formId = await api.CreateFormAsync(SubmissionFlatteningWorld.DefinitionJson);
        var first = await api.SubmitCompleteAsync(formId, """{"q1":"one"}""");
        var second = await api.SubmitCompleteAsync(formId, """{"q1":"two"}""");
        await api.PostOkAsync($"/api/forms/{formId}/reporting/compile-schema", new { });
        await api.PostOkAsync($"/api/forms/{formId}/submissions/backfill", new { force = true });
        await AssertDownloadIdsAsync(api, formId, [first, second]);

        using var pause = host.Services.GetRequiredService<AppCommandGate>().PauseAt(AnswerWrite());
        var delivery = BackgroundWork.Of(host.Services).DeliverOutboxOnceAsync(ct);
        try
        {
            await pause.Reached.WaitAsync(TimeSpan.FromSeconds(30), ct);
            await AssertDownloadIdsAsync(api, formId, [first, second]);
        }
        finally
        {
            pause.Release();
            await delivery;
        }

        await AssertDownloadIdsAsync(api, formId, [first, second]);
    }

    [Fact]
    public async Task Forced_backfill_rebuilds_a_prepared_response_against_the_merged_schema()
    {
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        ScenarioApi api = new(client, ct);
        var formId = await api.CreateFormAsync(SubmissionFlatteningWorld.DefinitionJson);
        await api.SubmitCompleteAsync(formId, """{"q1":"one","extra":"retained answer"}""");
        var compile = $"/api/forms/{formId}/reporting/compile-schema";
        var backfill = $"/api/forms/{formId}/submissions/backfill";
        await api.PostOkAsync(compile, new { });
        await api.PostOkAsync(backfill, new { force = true });
        (await api.ExportCsvAsync(formId)).Should().NotContain("retained answer");
        await api.PublishDefinitionAsync(formId, """{"elements":[{"type":"text","name":"q1"},{"type":"text","name":"extra"}]}""");
        await api.PostOkAsync(compile, new { });

        await api.PostOkAsync(backfill, new { force = true });

        (await api.ExportCsvAsync(formId)).Should().Contain("retained answer");
    }

    private static async Task AssertDownloadIdsAsync(ScenarioApi api, string formId, string[] expected)
    {
        using AssertionScope downloads = new();
        foreach (var profile in new[] { "csv", "csv-shoji" })
        {
            using var reader = new StringReader((await api.ExportCsvAsync(formId, profile)).TrimStart('\uFEFF'));
            using var csv = CsvReader.Create(reader, new CsvReaderOptions { HasHeader = true });
            List<string> ids = [];
            while (csv.Read())
            {
                ids.Add(csv.GetString("Id"));
            }

            ids.Should().Equal(expected, profile);
        }
    }

    [GeneratedRegex("""^UPDATE\b.*SET\s+.*["\[]DataJson["\]]\s*=""", RegexOptions.Singleline)]
    private static partial Regex AnswerWrite();
}
