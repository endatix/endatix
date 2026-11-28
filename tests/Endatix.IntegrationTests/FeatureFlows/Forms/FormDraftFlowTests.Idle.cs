using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Repositories;
using Endatix.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

public sealed partial class FormDraftFlowTests
{
    [Fact]
    public async Task Idle_checkpoint_at_one_day_resets_the_cursor_without_changing_either_document()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        using var saved = await FirstBatchAsync(editor, form);
        var before = await ReadHeadAsync(editor, form);
        var due = before.GetProperty("lastModifiedAt").GetDateTimeOffset().AddDays(1);
        var live = await editor.GetStringAsync($"{form}/definition", Cancellation);

        // Act
        await ScanAsync(due.AddMilliseconds(-1));
        using var early = await editor.GetAsync($"{form}/draft", Cancellation);
        await ScanAsync(due);
        await ScanAsync(due.AddDays(1));
        using var stale = await AppendBatchAsync(editor, form, "another edit");

        // Assert
        early.Headers.ETag.Should().Be(saved.Headers.ETag);
        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        stale.Headers.ETag!.Tag.Should().Be("\"4\"");
        var problem = await stale.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var checkpoint = problem.GetProperty("draft");
        checkpoint.GetProperty("jsonData").GetString().Should().Be(before.GetProperty("jsonData").GetString());
        checkpoint.GetProperty("contentHash").GetString().Should().Be(before.GetProperty("contentHash").GetString());
        checkpoint.GetProperty("recordsSinceSnapshot").GetInt32().Should().Be(0);
        checkpoint.GetProperty("lastModifiedBy").GetProperty("kind").GetString().Should().Be("system");
        (await editor.GetStringAsync($"{form}/definition", Cancellation)).Should().Be(live);
    }

    [Theory]
    [InlineData("whole")]
    [InlineData("discard")]
    [InlineData("periodic")]
    public async Task A_head_already_covered_by_a_snapshot_keeps_its_ETag(string write)
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var saved = write switch
        {
            "whole" => await editor.PutAsJsonAsync($"{form}/draft", new { jsonData = EDITED }, Cancellation),
            "discard" => await editor.DeleteAsync($"{form}/draft", Cancellation),
            _ => await editor.PatchAsJsonAsync($"{form}/draft", CheckpointBatch(200, []), Cancellation)
        };
        saved.EnsureSuccessStatusCode();
        var before = await ReadHeadAsync(editor, form);

        // Act
        await ScanAsync(before.GetProperty("lastModifiedAt").GetDateTimeOffset().AddDays(2));

        // Assert
        using var read = await editor.GetAsync($"{form}/draft", Cancellation);
        read.Headers.ETag.Should().Be(saved.Headers.ETag);
        before.GetProperty("recordsSinceSnapshot").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_idle_writer_concedes_to_a_concurrent_edit_or_checkpoint(bool editorWins)
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        using var saved = await FirstBatchAsync(editor, form);
        var before = await ReadHeadAsync(editor, form);
        var due = before.GetProperty("lastModifiedAt").GetDateTimeOffset().AddDays(1);
        using var pause = fixture.Factory.Services.GetRequiredService<AppCommandGate>().PauseAt(DraftHeadUpdate());
        var scanning = ScanAsync(due);
        await pause.Reached.WaitAsync(TimeSpan.FromSeconds(15), Cancellation);

        // Act
        if (editorWins)
        {
            using var edited = await AppendBatchAsync(editor, form, "winner");
            edited.EnsureSuccessStatusCode();
        }
        else
        {
            await ScanAsync(due);
        }
        pause.Release();
        await scanning;

        // Assert
        var head = await ReadHeadAsync(editor, form);
        head.GetProperty("version").GetString().Should().Be(editorWins ? "5" : "4");
        head.GetProperty("recordsSinceSnapshot").GetInt32().Should().Be(editorWins ? 4 : 0);
        head.GetProperty("jsonData").GetString().Should().Be(editorWins ? """{"title":"winner"}""" : """{"title":"first"}""");
    }

    private static async Task<HttpResponseMessage> FirstBatchAsync(HttpClient editor, string form)
    {
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        var saved = await AppendBatchAsync(editor, form, "first");
        saved.EnsureSuccessStatusCode();
        editor.DefaultRequestHeaders.IfMatch.Clear();
        editor.DefaultRequestHeaders.IfMatch.Add(saved.Headers.ETag!);
        return saved;
    }

    private static Task<JsonElement> ReadHeadAsync(HttpClient editor, string form) =>
        editor.GetFromJsonAsync<JsonElement>($"{form}/draft", Cancellation);

    private async Task ScanAsync(DateTimeOffset at)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var drafts = scope.ServiceProvider.GetRequiredService<IFormDraftRepository>();
        await new IdleDraftCheckpoints(db, drafts, new FixedClock(at)).RunAsync(Cancellation);
    }

    private sealed record FixedClock(DateTimeOffset Now) : IDateTimeProvider;
}
