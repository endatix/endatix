using System.Net;
using System.Text.RegularExpressions;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A write to a submission that read it before its form's deletion and saves after: the save matches no row, so the
/// write is not found and leaves nothing, no change, no version and no event, behind the deletion.
/// </summary>
public sealed partial class FormDeletionFlowTests
{
    [Theory]
    [MemberData(nameof(RacingSubmissionWrites))]
    public async Task Writing_to_a_submission_while_its_form_is_deleted_is_not_found_and_leaves_nothing(
        string write)
    {
        // Arrange
        var (world, admin) = await AdminAsync();
        var formId = await CreateFormAsync(admin);
        var target = await WriteTargetAsync(world.Services, admin, formId);
        var submissionId = target.SubmissionId;
        var since = DateTime.UtcNow;
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(Statement(RacingWrites[write].PauseAt));
        using var request = WriteRequest(RacingWrites[write], target);
        var writing = requests.Add(admin.SendAsync(request, Ct));
        await ReachedAsync(pause, writing);
        // Read at the pause: a by-token write's token extend before the answers' save is not the save that races.
        var before = await StateAsync(world.Services, submissionId);
        using var deleted = await admin.DeleteAsync($"/api/forms/{formId}", Ct).WaitAsync(RequestTimeout, Ct);

        // Act
        pause.Release();
        using var written = await writing.WaitAsync(RequestTimeout, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, written.StatusCode);
        Assert.Equal(new Dictionary<long, bool> { [submissionId] = true }, await DeletedByIdAsync(world.Services, formId));
        Assert.Equal(before, await StateAsync(world.Services, submissionId));
        Assert.Equal(0, await VersionCountAsync(world.Services, submissionId));
        Assert.Empty(await EventTypesNamingAsync(world.Services, submissionId, since));
    }

    /// <summary>Singleline, so a pattern can span the lines of a batched statement.</summary>
    private static Regex Statement(string pattern) =>
        new(pattern, RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    private static async Task<int> VersionCountAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await app.SubmissionVersions.IgnoreQueryFilters().CountAsync(row => row.SubmissionId == submissionId, Ct);
    }
}
