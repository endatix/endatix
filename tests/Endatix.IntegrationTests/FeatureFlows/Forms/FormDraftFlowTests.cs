using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed partial class FormDraftFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string PUBLISHED = """{"title":"Live","pages":[]}""";
    private const string EDITED = """{"title":"Edited","pages":[]}""";
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_stored_draft_operation_cannot_be_changed_or_deleted(bool delete)
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, Cancellation);
        using var editor = await world.AsAsync(TestPersona.Creator, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: Cancellation);
        var form = await CreateFormAsync(editor);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var saved = await editor.PutAsJsonAsync($"{form}/draft", new { jsonData = EDITED }, Cancellation);
        saved.EnsureSuccessStatusCode();
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var operation = await db.FormDraftOperations.SingleAsync(row => row.Version == 2, Cancellation);
        if (delete)
        {
            db.Remove(operation);
        }
        else
        {
            db.Entry(operation).Property(row => row.Payload).CurrentValue = "{}";
        }

        // Act
        var changeHistory = () => db.SaveChangesAsync(Cancellation);

        // Assert
        await changeHistory.Should().ThrowAsync<InvalidOperationException>().WithMessage("*insert-only*");
    }

    [Fact]
    public async Task Two_first_batches_from_the_same_version_commit_only_the_winners_records()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");

        // Act
        var replies = await Task.WhenAll(AppendAsync("left"), AppendAsync("right"));
        using var first = replies[0];
        using var second = replies[1];

        // Assert
        replies.Select(response => response.StatusCode).Should().BeEquivalentTo(
            [HttpStatusCode.OK, HttpStatusCode.PreconditionFailed]);
        replies.Should().OnlyContain(response => response.Headers.ETag!.Tag == "\"3\"");
        using var read = await editor.GetAsync($"{form}/draft", Cancellation);
        read.Headers.ETag!.Tag.Should().Be("\"3\"");

        var winner = first.IsSuccessStatusCode ? "left" : "right";
        var json = await read.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        json.GetProperty("jsonData").GetString().Should().Be(JsonSerializer.Serialize(new { title = winner }));
        await AssertWinningHistoryAsync(form, winner);

        Task<HttpResponseMessage> AppendAsync(string title) => AppendBatchAsync(editor, form, title);
    }

    [Fact]
    public async Task Whole_json_save_is_conditional_and_does_not_change_the_published_definition()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        using var head = await editor.GetAsync($"{form}/draft", Cancellation);
        head.Headers.ETag!.Tag.Should().Be("\"0\"");

        // Act
        using var missing = await editor.PutAsJsonAsync($"{form}/draft", new { jsonData = EDITED }, Cancellation);
        editor.DefaultRequestHeaders.IfMatch.Add(head.Headers.ETag);
        using var saved = await editor.PutAsJsonAsync($"{form}/draft", new { jsonData = EDITED }, Cancellation);
        using var stale = await editor.PutAsJsonAsync($"{form}/draft", new { jsonData = PUBLISHED }, Cancellation);

        // Assert
        missing.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        saved.Headers.ETag!.Tag.Should().Be("\"2\"");
        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        stale.Headers.ETag.Should().Be(saved.Headers.ETag);
        var conflict = await stale.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        conflict.GetProperty("draft").GetProperty("jsonData").GetString().Should().Be(EDITED);
        var live = await editor.GetFromJsonAsync<JsonElement>($"{form}/definition", Cancellation);
        live.GetProperty("jsonData").ToString().Should().Contain("Live");
    }

    [Fact]
    public async Task Equal_put_is_a_no_op_but_equal_journal_records_and_discard_advance_the_head()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var unchanged = await editor.PutAsJsonAsync($"{form}/draft", new { jsonData = PUBLISHED }, Cancellation);
        var batch = new
        {
            saveNo = 7,
            clientSessionId = "session",
            jsonData = PUBLISHED,
            contentHash = new string('f', 64),
            records = new[] { new { seq = 1 }, new { seq = 1 } }
        };

        // Act
        using var appended = await editor.PatchAsJsonAsync($"{form}/draft", batch, Cancellation);
        var saved = await appended.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        editor.DefaultRequestHeaders.IfMatch.Clear();
        editor.DefaultRequestHeaders.IfMatch.Add(appended.Headers.ETag!);
        using var discarded = await editor.DeleteAsync($"{form}/draft", Cancellation);

        // Assert
        unchanged.Headers.ETag!.Tag.Should().Be("\"0\"");
        appended.Headers.ETag!.Tag.Should().Be("\"3\"");
        saved.GetProperty("saveNo").GetInt32().Should().Be(7);
        saved.GetProperty("contentHash").GetString().Should().NotBe(batch.contentHash);
        discarded.Headers.ETag!.Tag.Should().Be("\"4\"");
    }

    private async Task<HttpClient> EditorAsync()
    {
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, Cancellation);
        return await world.AsAsync(TestPersona.Creator, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: Cancellation);
    }

    private static async Task<string> CreateFormAsync(HttpClient client, string definition = PUBLISHED)
    {
        using var created = await client.PostAsJsonAsync("/api/forms",
            new { name = $"draft-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = definition }, Cancellation);
        created.EnsureSuccessStatusCode();
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        return $"/api/forms/{body.GetProperty("id").GetString()}";
    }
}
