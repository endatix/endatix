using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class ScenarioCsvExportFlowTests
{
    private const string REBUILD_SCHEMA = """{"elements":[{"type":"text","name":"q1"},{"type":"text","name":"extra"}]}""";
    private const string LATER_SCHEMA = """{"elements":[{"type":"text","name":"q1"},{"type":"text","name":"extra"},{"type":"text","name":"later"}]}""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Force_reports_its_lost_claim_whether_later_completion_publishes_or_fails(bool faultLater)
    {
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await ReplayClientAsync(host, ct);
        await EnableShojiAsync(client, ct);
        ScenarioApi api = new(client, ct);
        var form = await api.CreateFormAsync(SubmissionFlatteningWorld.DefinitionJson);
        var id = await api.SubmitCompleteAsync(form, """{"q1":"one","extra":"rebuilt value","later":"winner value"}""");
        await CompileVersionAsync(api, form, REBUILD_SCHEMA);
        var uri = $"/api/forms/{form}/submissions/backfill";
        var gate = host.Services.GetRequiredService<AppCommandGate>();
        using var pause = gate.PauseAt(AnswerWrite());

        // Pause after force has read B. Only the next claimant can have computed the extra "later" field.
        var force = client.PostAsJsonAsync(uri, new { force = true }, ct);
        try
        {
            await ReachedBeforeDeliveryEndsAsync(pause.Reached, force, ct);
            await CompileVersionAsync(api, form, LATER_SCHEMA);
            await DeliverLaterClaimAsync(host, faultLater, ct);
            await AssertWinnerDownloadsAsync(api, form, !faultLater);
        }
        finally
        {
            pause.Release();
        }

        using var response = await force.WaitAsync(TimeSpan.FromSeconds(30), ct);
        await AssertForceLostAsync(response, id, ct);
        await AssertWinnerDownloadsAsync(api, form, !faultLater);
        if (faultLater)
        {
            await api.PostOkAsync(uri, new { force = true });
            await AssertWinnerDownloadsAsync(api, form, true);
        }
    }

    private static async Task CompileVersionAsync(ScenarioApi api, string form, string definition)
    {
        await api.PublishDefinitionAsync(form, definition);
        await api.PostOkAsync($"/api/forms/{form}/reporting/compile-schema", new { });
    }

    [Fact]
    public async Task Non_forced_backfill_skips_the_row_published_by_the_later_completion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await ReplayClientAsync(host, ct);
        ScenarioApi api = new(client, ct);
        var form = await api.CreateFormAsync(SubmissionFlatteningWorld.DefinitionJson);
        await api.SubmitCompleteAsync(form, """{"q1":"published by completion"}""");
        await api.PostOkAsync($"/api/forms/{form}/reporting/compile-schema", new { });
        using var pause = host.Services.GetRequiredService<AppCommandGate>().PauseAt(AnswerWrite());
        var backfill = client.PostAsJsonAsync($"/api/forms/{form}/submissions/backfill", new { force = false }, ct);
        try
        {
            await ReachedBeforeDeliveryEndsAsync(pause.Reached, backfill, ct);
            await BackgroundWork.Of(host.Services).DeliverOutboxOnceAsync(ct);
            (await api.ExportCsvAsync(form)).Should().Contain("published by completion");
        }
        finally
        {
            pause.Release();
        }

        using var response = await backfill.WaitAsync(TimeSpan.FromSeconds(30), ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        (page.GetProperty("scanned").GetInt32(), page.GetProperty("processed").GetInt32(),
            page.GetProperty("skipped").GetInt32(), page.GetProperty("failed").GetInt32()).Should().Be((1, 0, 1, 0));
        page.GetProperty("failedSubmissionIds").GetArrayLength().Should().Be(0);
        (await api.ExportCsvAsync(form)).Should().Contain("published by completion");
    }

    private static async Task DeliverLaterClaimAsync(ReportingWebHost host, bool faultLater, CancellationToken ct)
    {
        var gate = host.Services.GetRequiredService<AppCommandGate>();
        var fault = new InvalidOperationException("Interrupted after claiming");
        using var interrupted = faultLater ? gate.FailAt(AnswerWrite(), fault) : null;
        var error = await Record.ExceptionAsync(() => BackgroundWork.Of(host.Services).DeliverOutboxOnceAsync(ct));
        Assert.Same(faultLater ? fault : null, error?.GetBaseException());
        (await SentCompletionsAsync(host, ct)).Should().HaveCount(faultLater ? 0 : 1);
    }

    private static async Task AssertForceLostAsync(HttpResponseMessage response, string id, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        TestContext.Current.TestOutputHelper!.WriteLine($"Force response: {page}");
        (page.GetProperty("scanned").GetInt32(), page.GetProperty("processed").GetInt32(),
            page.GetProperty("skipped").GetInt32(), page.GetProperty("failed").GetInt32()).Should().Be((1, 0, 0, 1));
        page.GetProperty("failedSubmissionIds").EnumerateArray().Select(item => item.ToString()).Should().Equal(id);
    }

    private static async Task AssertWinnerDownloadsAsync(ScenarioApi api, string form, bool published)
    {
        foreach (var profile in new[] { "csv", "csv-shoji" })
        {
            using var response = await api.RequestCsvAsync(form, profile);
            response.StatusCode.Should().Be(published ? HttpStatusCode.OK : HttpStatusCode.Conflict);
            var csv = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            TestContext.Current.TestOutputHelper!.WriteLine($"{profile} {response.StatusCode}:\n{csv}");
            if (!published)
            {
                using var problem = JsonDocument.Parse(csv);
                problem.RootElement.GetProperty("detail").GetString().Should().Contain("No processed flattened submissions");
            }

            csv.Contains("rebuilt value", StringComparison.Ordinal).Should().Be(published);
            csv.Contains("winner value", StringComparison.Ordinal).Should().Be(published);
        }
    }
}
