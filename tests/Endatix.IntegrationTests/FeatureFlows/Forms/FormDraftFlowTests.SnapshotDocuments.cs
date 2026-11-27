using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Versioning;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

public sealed partial class FormDraftFlowTests
{
    [Theory]
    [InlineData("""{"title":"\uD800"}""", false)]
    [InlineData("""{"nested":[{"\uDC00":1}]}""", false)]
    [InlineData("""{"title":"\uD800"}""", true)]
    public async Task Invalid_survey_string_encoding_returns_validation_without_committing_a_batch(string invalid, bool atHead)
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        var batch = SurveyBatch(atHead ? invalid : EDITED, atHead ? EDITED : invalid);

        // Act
        using var refused = await editor.PatchAsJsonAsync($"{form}/draft", batch, Cancellation);

        // Assert
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("fields").TryGetProperty(atHead ? "jsonData" : "checkpoints", out _).Should().BeTrue();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.FormDrafts.AnyAsync(Cancellation)).Should().BeFalse();
        (await db.FormDraftOperations.AnyAsync(Cancellation)).Should().BeFalse();
    }

    [Fact]
    public async Task Reloaded_snapshot_documents_verify_against_the_hash_of_the_supplied_survey()
    {
        // Arrange
        const string interior = """{"z":1.2300e-05,"a":-0}""";
        const string terminal = """{"z":1E+02,"a":-0.00}""";
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");

        // Act
        using var saved = await editor.PatchAsJsonAsync($"{form}/draft", SurveyBatch(terminal, interior), Cancellation);
        saved.EnsureSuccessStatusCode();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.FormDraftOperations.AsNoTracking().Where(row => row.Reason == "periodic")
            .OrderBy(row => row.Version).ToArrayAsync(Cancellation);

        // Assert
        rows.Select(row => row.Version).Should().Equal(202, 403);
        rows.Select(row => row.ContentHash).Should().Equal(
            CanonicalContentHash.Of("""{"a":-0,"z":1.2300e-05}"""),
            CanonicalContentHash.Of("""{"a":-0.00,"z":1E+02}"""));
        foreach (var row in rows)
        {
            CanonicalSurveyJson.Hash(row.Payload).Should().Be(row.ContentHash);
        }
        var head = await db.FormDrafts.AsNoTracking().SingleAsync(Cancellation);
        head.ContentHash.Should().Be(rows[^1].ContentHash);
    }

    private static object SurveyBatch(string terminal, string interior) => new
    {
        records = Enumerable.Range(1, 400).Select(seq => new { seq }),
        jsonData = terminal,
        checkpoints = new[] { new { afterRecord = 200, jsonData = interior } },
        saveNo = 1,
        clientSessionId = "snapshot-test",
        contentHash = new string('f', 64)
    };
}
