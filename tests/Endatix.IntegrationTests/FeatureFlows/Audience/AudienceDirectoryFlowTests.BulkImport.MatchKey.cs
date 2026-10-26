using System.Net;
using System.Text.Json;

namespace Endatix.IntegrationTests;

/// <summary>A checked Participant import whose match key changes before the write.</summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Changing_the_match_key_after_the_check_fails_the_write_and_adds_no_one()
    {
        // Arrange — the Check has pinned email, and the write has added no one, so the key can still change.
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 2).Select(n => new[] { $"p{n}@example.com" })];
        BulkImport import = await api.StageImportAsync(formId, ["email"], rows);
        Assert.Equal("checked", (await api.WaitForImportAsync(import, "checked")).GetProperty("status").GetString());

        // Act
        using HttpResponseMessage key = await api.PutKeyAsync("external_id");
        using HttpResponseMessage confirmed = await api.MoveImportAsync(import, "writing");
        JsonElement finished = await api.WaitForImportAsync(import, "failed");

        // Assert
        Assert.Equal(HttpStatusCode.OK, key.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("failed", finished.GetProperty("status").GetString());
        Assert.Equal(
            "The match key changed after the check. Start the import again.",
            finished.GetProperty("failure").GetString());
        Assert.Empty(await api.IdentifiersAsync(formId));
    }
}
