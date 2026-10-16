using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Endatix.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A create and the form's delete that overlap, forced by pausing one of them at a statement while the other runs.
/// Whichever goes first, the form ends with no live submission.
/// </summary>
public sealed partial class FormDeletionFlowTests
{
    /// <summary>Long enough for a request that does not wait to finish; one that waits stays paused until released.</summary>
    private static readonly TimeSpan WaitProbe = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Creating_a_submission_while_its_form_is_being_deleted_is_not_found()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(FormUpdate());
        var deleting = requests.Add(admin.DeleteAsync($"/api/forms/{formId}", Ct));
        await ReachedAsync(pause, deleting);

        // Act
        var creating = requests.Add(admin.PostAsJsonAsync($"/api/forms/{formId}/submissions", DraftSubmission, Ct));
        var createWaited = await WaitedAsync(creating);
        pause.Release();
        using var deleted = await deleting.WaitAsync(RequestTimeout, Ct);
        using var created = await creating.WaitAsync(RequestTimeout, Ct);

        // Assert
        Assert.Empty(await DeletedByIdAsync(world.Services, formId));
        Assert.Equal(HttpStatusCode.NotFound, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(createWaited, "The create did not wait for the form's delete.");
    }

    [Fact]
    public async Task Deleting_a_form_while_a_submission_is_being_created_deletes_that_submission_too()
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(SubmissionInsert());
        var creating = requests.Add(admin.PostAsJsonAsync($"/api/forms/{formId}/submissions", DraftSubmission, Ct));
        await ReachedAsync(pause, creating);

        // Act
        var deleting = requests.Add(admin.DeleteAsync($"/api/forms/{formId}", Ct));
        var deleteWaited = await WaitedAsync(deleting);
        pause.Release();
        using var created = await creating.WaitAsync(RequestTimeout, Ct);
        using var deleted = await deleting.WaitAsync(RequestTimeout, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = await IdAsync(created);
        Assert.Equal(new Dictionary<long, bool> { [submissionId] = true }, await DeletedByIdAsync(world.Services, formId));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(deleteWaited, "The delete did not wait for the create under way.");
    }

    private static async Task ReachedAsync(CommandPause pause, Task paused)
    {
        await Task.WhenAny(pause.Reached, paused).WaitAsync(RequestTimeout, Ct);
        Assert.True(pause.Reached.IsCompleted, "The first request never reached its pause.");
    }

    private static async Task<bool> WaitedAsync(Task request) =>
        await Task.WhenAny(request, Task.Delay(WaitProbe, Ct)) != request;

    /// <summary>PostgreSQL quotes the table <c>"Forms"</c>, SQL Server <c>[Forms]</c>.</summary>
    [GeneratedRegex("""UPDATE ["\[]Forms["\]]""")]
    private static partial Regex FormUpdate();

    [GeneratedRegex("""INSERT INTO ["\[]Submissions["\]]""")]
    private static partial Regex SubmissionInsert();
}
