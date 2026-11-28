using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

public sealed partial class FormDraftFlowTests
{
    [Fact]
    public Task Unsupported_legacy_array_root_returns_a_blocking_report_without_creating_a_head() =>
        AssertStoredRootBlockedAsync("[]");

    // PostgreSQL jsonb can store a JSON null; SQL Server's json column rejects it before the endpoint runs.
    [Fact]
    [Trait("DbSpecific", "PostgreSql")]
    public Task Stored_json_null_returns_a_blocking_report_without_creating_a_head() =>
        AssertStoredRootBlockedAsync("null");

    private async Task AssertStoredRootBlockedAsync(string json)
    {
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = FormId(form);
        var definitions = db.FormDefinitions.Where(row => row.FormId == id);
        await definitions.ExecuteUpdateAsync(update => update.SetProperty(row => row.JsonData, json), Cancellation);
        (await definitions.Select(row => row.JsonData).SingleAsync(Cancellation)).Should().Be(json);

        var report = await CheckAsync(editor, form);

        report.GetProperty("draftVersion").GetString().Should().Be("0");
        AssertShapeFinding(report, "");
        (await db.FormDrafts.AnyAsync(head => head.FormId == id, Cancellation)).Should().BeFalse();
    }

    [Fact]
    public async Task Unsupported_members_block_at_their_original_locations_without_losing_valid_siblings()
    {
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        var jsonData = """
            {"pages":[null,{"elements":[{"type":"panel","elements":false},3,{"type":"text","name":"a"},
             {"type":"text","name":"a"},{"type":"unknown","name":"z"}]}],"calculatedValues":[false]}
            """;
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var saved = await editor.PutAsJsonAsync(form + "/draft", new { jsonData }, Cancellation);
        saved.EnsureSuccessStatusCode();
        var draft = await saved.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        editor.DefaultRequestHeaders.IfMatch.Clear();

        var report = await CheckAsync(editor, form);

        AssertShapeFinding(report, "/pages/index:0");
        AssertShapeFinding(report, "/pages/index:1/elements/index:0/elements");
        AssertShapeFinding(report, "/pages/index:1/elements/index:1");
        AssertShapeFinding(report, "/calculatedValues/index:0");
        var keys = report.GetProperty("findings").EnumerateArray().Select(f => f.GetProperty("key").GetString());
        keys.Should().Contain(["column/duplicate-key:a", "element/unknown-type:/pages/index:1/elements/index:4/type"]);
        report.GetProperty("contentHash").GetString().Should().Be(draft.GetProperty("contentHash").GetString());
        (await CheckAsync(editor, form)).GetRawText().Should().Be(report.GetRawText());
    }

    private static void AssertShapeFinding(JsonElement report, string location)
    {
        report.GetProperty("outcome").GetString().Should().Be("block");
        var finding = report.GetProperty("findings").EnumerateArray()
            .Single(f => f.GetProperty("key").GetString() == $"definition/invalid-shape:{location}");
        finding.GetProperty("requiresAck").GetBoolean().Should().BeFalse();
        finding.GetProperty("blocking").GetBoolean().Should().BeTrue();
    }
}
