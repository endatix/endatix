using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// What a create reads again once it holds the form's lock, and what a tracked write leaves alone: a write that sets
/// only what changed takes no lock on the form's rows, which the form's deletion takes after the submissions'.
/// </summary>
public sealed partial class FormDeletionFlowTests
{
    [Fact]
    public async Task Creating_a_submission_of_a_form_disabled_after_its_first_check_is_not_found()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(LockStatement());
        var creating = requests.Add(admin.PostAsJsonAsync($"/api/forms/{formId}/submissions", DraftSubmission, Ct));
        await ReachedAsync(pause, creating);
        using var disabled = await admin.PatchAsJsonAsync($"/api/forms/{formId}", new { isEnabled = false }, Ct);

        // Act
        pause.Release();
        using var created = await creating.WaitAsync(RequestTimeout, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, created.StatusCode);
        Assert.Empty(await DeletedByIdAsync(world.Services, formId));
    }

    [Fact]
    public async Task Creating_a_submission_after_a_definition_is_published_meanwhile_uses_that_definition()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(LockStatement());
        var creating = requests.Add(admin.PostAsJsonAsync($"/api/forms/{formId}/submissions", DraftSubmission, Ct));
        await ReachedAsync(pause, creating);
        using var published = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/definitions",
            new { isDraft = false, jsonData = FormDefinition },
            Ct);
        Assert.Equal(HttpStatusCode.Created, published.StatusCode);
        var definitionId = await IdAsync(published);

        // Act
        pause.Release();
        using var created = await creating.WaitAsync(RequestTimeout, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(definitionId, long.Parse(body.GetProperty("formDefinitionId").GetString()!, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("POST status")]
    [InlineData("DELETE")]
    public async Task Writing_a_submission_sets_none_of_its_form_keys(string write)
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        var target = await WriteTargetAsync(world.Services, admin, formId);
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(FormKeyWrite());
        using var request = WriteRequest(ChangingWrites[write], target);

        // Act
        var writing = requests.Add(admin.SendAsync(request, Ct));
        await Task.WhenAny(writing, pause.Reached).WaitAsync(RequestTimeout, Ct);

        // Assert
        Assert.False(pause.Reached.IsCompleted, "The write set the submission's form or definition key.");
        using var written = await writing;
        Assert.True(written.IsSuccessStatusCode, $"The write answered {(int)written.StatusCode}.");
    }

    /// <summary>A statement that sets a submission's form or definition key, which the database checks against the form's rows.</summary>
    [GeneratedRegex("""UPDATE ["\[]Submissions["\]] SET .*["\[](FormId|FormDefinitionId)["\]]""", RegexOptions.Singleline)]
    private static partial Regex FormKeyWrite();
}
