using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Deleting a form's test submissions, page by page, while the form, or one of those submissions, is deleted by
/// another request: the page that loses a row saves nothing and is read again, and a deleted form is not found.
/// </summary>
public sealed partial class FormDeletionFlowTests
{
    [Fact]
    public async Task Deleting_a_forms_test_submissions_while_the_form_is_deleted_is_not_found_and_raises_nothing()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        var submissionIds = await TestSubmissionsAsync(admin, formId, count: 2);
        var since = DateTime.UtcNow;
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(Statement(FirstSubmissionSave));
        var clearing = requests.Add(ClearTestsAsync(admin, formId));
        await ReachedAsync(pause, clearing);
        using var deleted = await admin.DeleteAsync($"/api/forms/{formId}", Ct).WaitAsync(RequestTimeout, Ct);

        // Act
        pause.Release();
        using var cleared = await clearing.WaitAsync(RequestTimeout, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cleared.StatusCode);
        Assert.Equal(submissionIds.ToDictionary(id => id, _ => true), await DeletedByIdAsync(world.Services, formId));
        foreach (var submissionId in submissionIds)
        {
            Assert.Empty(await EventTypesNamingAsync(world.Services, submissionId, since));
        }
    }

    [Fact]
    public async Task Deleting_a_forms_test_submissions_while_one_is_deleted_on_its_own_deletes_the_rest()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        var submissionIds = await TestSubmissionsAsync(admin, formId, count: 3);
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(Statement(FirstSubmissionSave));
        var clearing = requests.Add(ClearTestsAsync(admin, formId));
        await ReachedAsync(pause, clearing);
        using var deletedOne = await admin.DeleteAsync($"/api/forms/{formId}/submissions/{submissionIds[0]}", Ct)
            .WaitAsync(RequestTimeout, Ct);

        // Act
        pause.Release();
        using var cleared = await clearing.WaitAsync(RequestTimeout, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deletedOne.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var body = await cleared.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(2, body.GetProperty("deletedCount").GetInt32());
        Assert.Equal(submissionIds.ToDictionary(id => id, _ => true), await DeletedByIdAsync(world.Services, formId));
    }

    private static async Task<long[]> TestSubmissionsAsync(HttpClient admin, long formId, int count)
    {
        var ids = new long[count];
        for (var index = 0; index < count; index++)
        {
            ids[index] = await CreateSubmissionAsync(admin, formId);
            using var marked = await admin.PatchAsJsonAsync(
                $"/api/forms/{formId}/submissions/{ids[index]}/test", new { isTest = true }, Ct);
            Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        }

        return ids;
    }

    private static Task<HttpResponseMessage> ClearTestsAsync(HttpClient admin, long formId) =>
        admin.PostAsJsonAsync($"/api/forms/{formId}/submissions/test/delete", new { }, Ct);
}
