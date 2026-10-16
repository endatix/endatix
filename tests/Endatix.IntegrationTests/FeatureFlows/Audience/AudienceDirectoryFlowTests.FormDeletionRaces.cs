using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The first open of a personalised link and its form's delete that overlap, forced by pausing one of them at a
/// statement. Whichever goes first, the form ends with no live submission.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    /// <summary>The open does not queue behind the delete with a connection held: it is refused at once.</summary>
    [Fact]
    public async Task Opening_a_link_while_its_form_is_being_deleted_is_unavailable_at_once()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Akron");
        using var anonymous = world.AnonymousClient();
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(FormUpdate());
        var deleting = requests.Add(admin.Client.DeleteAsync($"/api/forms/{formId}", admin.CancellationToken));
        await PausedAsync(pause, deleting);

        // Act
        var opening = requests.Add(
            anonymous.PostAsJsonAsync(RedeemUrl(formId, token), new { }, admin.CancellationToken));
        var openWaited = await Task.WhenAny(opening, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != opening;
        pause.Release();
        using var deleted = await deleting.WaitAsync(_gateTimeout, admin.CancellationToken);
        using var opened = await opening.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Empty(await SubmissionsOfAsync(world.Services, formId));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, opened.StatusCode);
        Assert.False(openWaited, "The open waited for the form's delete.");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_form_while_its_link_is_being_opened_deletes_the_opened_submission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Dayton");
        using var anonymous = world.AnonymousClient();
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(OpenTokenSave());
        var opening = requests.Add(
            anonymous.PostAsJsonAsync(RedeemUrl(formId, token), new { }, admin.CancellationToken));
        await PausedAsync(pause, opening);

        // Act
        var deleting = requests.Add(admin.Client.DeleteAsync($"/api/forms/{formId}", admin.CancellationToken));
        var deleteWaited = await Task.WhenAny(deleting, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != deleting;
        pause.Release();
        using var opened = await opening.WaitAsync(_gateTimeout, admin.CancellationToken);
        using var deleted = await deleting.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        var submissionId = IdOf(await opened.Content.ReadFromJsonAsync<JsonElement>(admin.CancellationToken), "submissionId");
        Assert.Equal(
            new Dictionary<long, bool> { [submissionId] = true },
            await SubmissionsOfAsync(world.Services, formId));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(deleteWaited, "The delete did not wait for the open under way.");
    }

    private static async Task PausedAsync(CommandPause pause, Task paused)
    {
        await Task.WhenAny(pause.Reached, paused).WaitAsync(_gateTimeout, TestContext.Current.CancellationToken);
        Assert.True(pause.Reached.IsCompleted, "The first request never reached its pause.");
    }

    /// <summary>Whether each submission of the form is soft-deleted, by id.</summary>
    private static async Task<Dictionary<long, bool>> SubmissionsOfAsync(IServiceProvider services, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await app.Submissions.AsNoTracking()
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete, EndatixQueryFilterNames.Tenant])
            .Where(item => item.FormId == formId)
            .ToDictionaryAsync(item => item.Id, item => item.IsDeleted, TestContext.Current.CancellationToken);
    }

    /// <summary>PostgreSQL quotes the table <c>"Forms"</c>, SQL Server <c>[Forms]</c>.</summary>
    [GeneratedRegex("""UPDATE ["\[]Forms["\]]""")]
    private static partial Regex FormUpdate();

    /// <summary>The open's save of its new submission's token: after it read the form, before it commits.</summary>
    [GeneratedRegex("""UPDATE ["\[]Submissions["\]]""")]
    private static partial Regex OpenTokenSave();
}
