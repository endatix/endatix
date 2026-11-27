using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

public sealed partial class FormDraftFlowTests
{
    [Fact]
    public async Task One_401_edit_save_records_both_interior_documents_before_the_terminal_head()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        var body = CheckpointBatch(401, [200, 400]);

        // Act
        using var saved = await editor.PatchAsJsonAsync($"{form}/draft", body, Cancellation);
        saved.EnsureSuccessStatusCode();

        // Assert
        saved.Headers.ETag!.Tag.Should().Be("\"404\"");
        var ack = await saved.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        ack.GetProperty("recordsSinceSnapshot").GetInt32().Should().Be(1);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snapshots = await db.FormDraftOperations.Where(row => row.Reason == "periodic")
            .OrderBy(row => row.Version).ToArrayAsync(Cancellation);
        snapshots.Select(row => row.Version).Should().Equal(202, 403);
        snapshots.Select(row => JsonSerializer.Deserialize<JsonElement>(row.Payload).GetProperty("edit").GetInt32())
            .Should().Equal(200, 400);
        var read = await editor.GetFromJsonAsync<JsonElement>($"{form}/draft", Cancellation);
        read.GetProperty("jsonData").GetString().Should().Be("""{"edit":401}""");
        read.GetProperty("recordsSinceSnapshot").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Omitting_a_required_document_rejects_the_entire_first_save()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");

        // Act
        using var refused = await editor.PatchAsJsonAsync($"{form}/draft", CheckpointBatch(401, [200]), Cancellation);

        // Assert
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var read = await editor.GetAsync($"{form}/draft", Cancellation);
        read.Headers.ETag!.Tag.Should().Be("\"0\"");
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.FormDraftOperations.AnyAsync(Cancellation)).Should().BeFalse();
        (await db.FormDrafts.AnyAsync(Cancellation)).Should().BeFalse();
    }

    private static object CheckpointBatch(int count, int[] offsets) => new
    {
        saveNo = 1,
        clientSessionId = "session",
        records = Enumerable.Range(1, count).Select(seq => new { seq }),
        jsonData = JsonSerializer.Serialize(new { edit = count }),
        contentHash = new string('a', 64),
        checkpoints = offsets.Select(afterRecord => new { afterRecord, jsonData = JsonSerializer.Serialize(new { edit = afterRecord }) })
    };

    [Fact]
    public async Task A_partial_interval_continues_into_the_next_batch_and_a_retry_returns_the_winning_cursor()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var first = await editor.PatchAsJsonAsync($"{form}/draft", CheckpointBatch(199, []), Cancellation);
        first.EnsureSuccessStatusCode();
        var previous = await first.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        previous.GetProperty("recordsSinceSnapshot").GetInt32().Should().Be(199);
        editor.DefaultRequestHeaders.IfMatch.Clear();
        editor.DefaultRequestHeaders.IfMatch.Add(first.Headers.ETag!);
        var batch = CheckpointBatch(201, [1]);

        // Act
        using var second = await editor.PatchAsJsonAsync($"{form}/draft", batch, Cancellation);
        second.EnsureSuccessStatusCode();
        using var retry = await editor.PatchAsJsonAsync($"{form}/draft", batch, Cancellation);

        // Assert
        second.Headers.ETag!.Tag.Should().Be("\"403\"");
        var ack = await second.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        ack.GetProperty("recordsSinceSnapshot").GetInt32().Should().Be(0);
        retry.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        retry.Headers.ETag.Should().Be(second.Headers.ETag);
        var problem = await retry.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("draft").GetProperty("recordsSinceSnapshot").GetInt32().Should().Be(0);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var terminal = await db.FormDraftOperations.SingleAsync(row => row.Version == 403, Cancellation);
        terminal.Reason.Should().Be("periodic");
        JsonSerializer.Deserialize<JsonElement>(terminal.Payload).GetProperty("edit").GetInt32().Should().Be(201);
    }
}
