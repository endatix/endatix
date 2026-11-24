using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class ScenarioCsvExportFlowTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Completion_replay_keeps_both_prepared_responses_in_each_csv_download(int priorWrites)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await ReplayClientAsync(host, ct);
        ScenarioApi api = new(client, ct);
        var pair = await PreparePairAsync(api, client, ct);
        await AssertPairDownloadAsync(api, pair.Form, pair.Ids);
        using var pause = host.Services.GetRequiredService<AppCommandGate>().PauseAt(AnswerWrite(), skip: priorWrites);

        // Act: the Reporting host drains its batch in order; both background relays stay off.
        var delivery = BackgroundWork.Of(host.Services).DeliverOutboxOnceAsync(ct);
        try
        {
            await ReachedBeforeDeliveryEndsAsync(pause.Reached, delivery, ct);
            var sent = await SentCompletionsAsync(host, ct);
            sent.Should().Equal(pair.Ids.Take(priorWrites));
            await AssertPairDownloadAsync(api, pair.Form, pair.Ids);
        }
        finally
        {
            pause.Release();
            await delivery.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }

        // Assert
        (await SentCompletionsAsync(host, ct)).Should().Equal(pair.Ids);
        await AssertPairDownloadAsync(api, pair.Form, pair.Ids);
    }

    [Fact]
    public async Task Failed_completion_refresh_preserves_the_download_and_the_delivery_can_retry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await ReplayClientAsync(host, ct);
        ScenarioApi api = new(client, ct);
        var pair = await PreparePairAsync(api, client, ct);
        var gate = host.Services.GetRequiredService<AppCommandGate>();
        var work = BackgroundWork.Of(host.Services);
        var fault = new InvalidOperationException("Synthetic answer write failure");
        using var broken = gate.FailAt(AnswerWrite(), fault);

        // Act: the store has not replaced the retained snapshot when its terminal write fails.
        var delivery = work.DeliverOutboxOnceAsync(ct);
        await ReachedBeforeDeliveryEndsAsync(broken.Reached, delivery, ct);
        using var next = gate.PauseAt(AnswerWrite());
        var error = await Record.ExceptionAsync(() => ReachedBeforeDeliveryEndsAsync(next.Reached, delivery, ct));
        next.Release();

        // Assert: the failure reaches delivery, not the prepared answers. Retry sends both events.
        Assert.Same(fault, error?.GetBaseException());
        (await SentCompletionsAsync(host, ct)).Should().BeEmpty();
        await AssertPairDownloadAsync(api, pair.Form, pair.Ids);
        await work.DeliverOutboxOnceAsync(ct);
        (await SentCompletionsAsync(host, ct)).Should().Equal(pair.Ids);
        await AssertPairDownloadAsync(api, pair.Form, pair.Ids);
    }

    private static async Task ReachedBeforeDeliveryEndsAsync(Task reached, Task delivery, CancellationToken ct)
    {
        if (await Task.WhenAny(reached, delivery).WaitAsync(TimeSpan.FromSeconds(30), ct) == delivery)
        {
            await delivery;
            Assert.Fail("Delivery finished without reaching the expected Reporting answer write.");
        }
    }

    private static async Task<string[]> SentCompletionsAsync(ReportingWebHost host, CancellationToken ct)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payloads = await db.OutboxMessages.Where(row => row.EventType == "submission.completed" && row.Status == OutboxMessageStatus.Sent)
            .OrderBy(row => row.Id).Select(row => row.Payload).ToListAsync(ct);
        return [.. payloads.Select(SubmissionIdFromPayload)];
    }

    private static string SubmissionIdFromPayload(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.GetProperty("submissionId").GetString()!;
    }

    private static async Task<HttpClient> ReplayClientAsync(ReportingWebHost host, CancellationToken ct)
    {
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
    }

    private static async Task<(string Form, string[] Ids)> PreparePairAsync(ScenarioApi api, HttpClient client, CancellationToken ct)
    {
        await EnableShojiAsync(client, ct);
        var form = await api.CreateFormAsync(SubmissionFlatteningWorld.DefinitionJson);
        var first = await api.SubmitCompleteAsync(form, """{"q1":"one"}""");
        var second = await api.SubmitCompleteAsync(form, """{"q1":"two"}""");
        await api.PostOkAsync($"/api/forms/{form}/reporting/compile-schema", new { });
        await api.PostOkAsync($"/api/forms/{form}/submissions/backfill", new { force = true });
        return (form, [first, second]);
    }

    private static async Task EnableShojiAsync(HttpClient client, CancellationToken ct)
    {
        using var format = await client.PostAsJsonAsync("/api/settings/export-formats",
            new { name = "Shoji CSV", exportTarget = "Submissions", deliveryFormat = "Csv", profile = "Shoji" }, ct);
        format.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("csv-shoji")]
    public async Task Forced_backfill_survives_a_late_completion_using_the_previous_schema(string profile)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await ReplayClientAsync(host, ct);
        await EnableShojiAsync(client, ct);
        ScenarioApi api = new(client, ct);
        var formId = await api.CreateFormAsync(SubmissionFlatteningWorld.DefinitionJson);
        await api.SubmitCompleteAsync(formId, """{"q1":"one","extra":"retained answer"}""");
        var compile = $"/api/forms/{formId}/reporting/compile-schema";
        var backfill = $"/api/forms/{formId}/submissions/backfill";
        await api.PostOkAsync(compile, new { });
        await api.PostOkAsync(backfill, new { force = true });
        (await ReadOverlapDownloadAsync(api, formId, profile)).Should().NotContain("retained answer");

        // Act
        using var pause = host.Services.GetRequiredService<AppCommandGate>().PauseAt(AnswerWrite());
        var delivery = BackgroundWork.Of(host.Services).DeliverOutboxOnceAsync(ct);
        try
        {
            await ReachedBeforeDeliveryEndsAsync(pause.Reached, delivery, ct);
            await api.PublishDefinitionAsync(formId, """{"elements":[{"type":"text","name":"q1"},{"type":"text","name":"extra"}]}""");
            await api.PostOkAsync(compile, new { });
            await api.PostOkAsync(backfill, new { force = true });
            (await ReadOverlapDownloadAsync(api, formId, profile)).Should().Contain("retained answer");
        }
        finally
        {
            pause.Release();
            await delivery.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }

        // Assert: no repair rebuild after the paused writer resumes.
        (await ReadOverlapDownloadAsync(api, formId, profile)).Should().Contain("retained answer");
        (await SentCompletionsAsync(host, ct)).Should().ContainSingle();
    }

    private static async Task<string> ReadOverlapDownloadAsync(ScenarioApi api, string formId, string profile)
    {
        var download = await api.ExportCsvAsync(formId, profile);
        TestContext.Current.TestOutputHelper!.WriteLine($"{profile} download:\n{download}");
        return download;
    }

    [Fact]
    public async Task Two_forced_backfills_report_the_first_attempts_lost_row()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await ReplayClientAsync(host, ct);
        ScenarioApi api = new(client, ct);
        var pair = await PreparePairAsync(api, client, ct);
        var uri = $"/api/forms/{pair.Form}/submissions/backfill";
        using var gate = host.Services.GetRequiredService<AppCommandGate>().PauseAt(AnswerWrite());

        // Act: a second backfill supersedes the first one's first row before publication.
        var first = client.PostAsJsonAsync(uri, new { force = true }, ct);
        try
        {
            await ReachedBeforeDeliveryEndsAsync(gate.Reached, first, ct);
            await api.PostOkAsync(uri, new { force = true });
        }
        finally
        {
            gate.Release();
        }

        // Assert: the first backfill still publishes the second row.
        using var response = await first.WaitAsync(TimeSpan.FromSeconds(30), ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        page.GetProperty("processed").GetInt32().Should().Be(1);
        page.GetProperty("skipped").GetInt32().Should().Be(0);
        page.GetProperty("failed").GetInt32().Should().Be(1);
        page.GetProperty("failedSubmissionIds").EnumerateArray().Select(id => id.ToString()).Should().Equal(pair.Ids[0]);
    }

    private static async Task AssertPairDownloadAsync(ScenarioApi api, string formId, string[] expected)
    {
        using AssertionScope downloads = new();
        foreach (var profile in new[] { "csv", "csv-shoji" })
        {
            using var reader = new StringReader((await api.ExportCsvAsync(formId, profile)).TrimStart('\uFEFF'));
            using var csv = CsvReader.Create(reader, new CsvReaderOptions { HasHeader = true });
            List<(string? Id, string? Answer)> rows = [];
            while (csv.Read())
            {
                rows.Add((csv.GetString("Id"), csv.GetString("q1")));
            }

            rows.Should().Equal([(expected[0], "one"), (expected[1], "two")], profile);
        }
    }

    [GeneratedRegex("""^UPDATE\b.*SET\s+.*["\[]DataJson["\]]\s*=""", RegexOptions.Singleline)]
    private static partial Regex AnswerWrite();
}
