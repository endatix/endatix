using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

public sealed partial class FormDraftFlowTests
{
    [Fact]
    public async Task Editor_session_labels_round_trip_at_the_limit_and_longer_labels_leave_history_untouched()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        var label = new string('é', 126) + "🌱";
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");

        // Act
        using var refused = await AppendBatchAsync(editor, form, label + "x");
        using var saved = await AppendBatchAsync(editor, form, label);

        // Assert
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("fields").EnumerateObject().Should().Contain(field =>
            field.Name.Equals("clientSessionId", StringComparison.OrdinalIgnoreCase));
        saved.EnsureSuccessStatusCode();
        await AssertWinningHistoryAsync(form, label);
    }

    [Fact]
    public async Task An_editor_can_repair_a_published_array_without_changing_the_live_document()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor, """[{"title":"Original"}]""");
        var live = await editor.GetFromJsonAsync<JsonElement>($"{form}/definition", Cancellation);
        var original = live.GetProperty("jsonData").GetString();

        // Act
        var draft = await editor.GetFromJsonAsync<JsonElement>($"{form}/draft", Cancellation);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var repaired = await editor.PutAsJsonAsync($"{form}/draft", new { jsonData = EDITED }, Cancellation);
        repaired.EnsureSuccessStatusCode();
        editor.DefaultRequestHeaders.IfMatch.Clear();
        editor.DefaultRequestHeaders.IfMatch.Add(repaired.Headers.ETag!);
        using var discarded = await editor.DeleteAsync($"{form}/draft", Cancellation);
        discarded.EnsureSuccessStatusCode();

        // Assert
        draft.GetProperty("jsonData").GetString().Should().Be(original);
        var corrected = await repaired.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        corrected.GetProperty("jsonData").GetString().Should().Be(EDITED);
        corrected.GetProperty("hasUnpublishedChanges").GetBoolean().Should().BeTrue();
        var restored = await discarded.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        restored.GetProperty("jsonData").GetString().Should().Be(original);
        restored.GetProperty("hasUnpublishedChanges").GetBoolean().Should().BeFalse();
        var stillLive = await editor.GetFromJsonAsync<JsonElement>($"{form}/definition", Cancellation);
        stillLive.GetProperty("jsonData").GetString().Should().Be(original);
    }

    [Theory]
    [InlineData("head")]
    [InlineData("history")]
    public async Task Failure_at_commit_leaves_the_preceding_head_and_history_intact(string write)
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var saved = await AppendBatchAsync(editor, form, "before");
        saved.EnsureSuccessStatusCode();
        editor.DefaultRequestHeaders.IfMatch.Clear();
        editor.DefaultRequestHeaders.IfMatch.Add(saved.Headers.ETag!);
        using var failure = fixture.Factory.Services.GetRequiredService<AppCommandGate>()
            .PauseAtCommitAfter(write == "head" ? DraftHeadUpdate() : DraftOperationsWrite(),
                new InvalidOperationException("Test draft commit failure."));
        failure.Release();

        // Act
        using var refused = await editor.PatchAsJsonAsync($"{form}/draft", CheckpointBatch(401, [198, 398]), Cancellation);

        // Assert
        failure.Reached.IsCompletedSuccessfully.Should().BeTrue("the database writes ran before commit was refused");
        refused.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        using var read = await editor.GetAsync($"{form}/draft", Cancellation);
        read.Headers.ETag.Should().Be(saved.Headers.ETag);
        var head = await read.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        head.GetProperty("jsonData").GetString().Should().Be("""{"title":"before"}""");
        await AssertWinningHistoryAsync(form, "before");
    }

    private static Task<HttpResponseMessage> AppendBatchAsync(HttpClient editor, string form, string title) =>
        editor.PatchAsJsonAsync($"{form}/draft", new
        {
            saveNo = 1,
            clientSessionId = title,
            jsonData = JsonSerializer.Serialize(new { title }),
            contentHash = new string('f', 64),
            records = new[] { new { title, seq = 1 }, new { title, seq = 2 } }
        }, Cancellation);

    private async Task AssertWinningHistoryAsync(string formPath, string winner)
    {
        var formId = long.Parse(formPath.Split('/')[^1], System.Globalization.CultureInfo.InvariantCulture);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.FormDraftOperations.AsNoTracking().Where(row => row.FormId == formId)
            .OrderBy(row => row.Version).ToListAsync(Cancellation);
        rows.Select(row => row.Version).Should().Equal(1, 2, 3);
        rows[0].Reason.Should().Be("seed");
        foreach (var row in rows.Skip(1))
        {
            using var payload = JsonDocument.Parse(row.Payload);
            row.EditorSessionId.Should().Be(winner);
            row.Channel.Should().Be("api");
            payload.RootElement.GetProperty("title").GetString().Should().Be(winner);
            payload.RootElement.GetProperty("seq").GetInt32().Should().Be((int)row.Version - 1);
        }
    }

    [GeneratedRegex(@"(?:INSERT|MERGE)\s+(?:INTO\s+)?[\[""]FormDraftOperations", RegexOptions.IgnoreCase)]
    private static partial Regex DraftOperationsWrite();

    [GeneratedRegex(@"UPDATE\s+[\[""]FormDrafts", RegexOptions.IgnoreCase)]
    private static partial Regex DraftHeadUpdate();
}
