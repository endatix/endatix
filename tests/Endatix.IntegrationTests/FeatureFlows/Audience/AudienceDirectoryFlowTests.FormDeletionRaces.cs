using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    /// <summary>
    /// A link claimed before its token was saved issues that token on a later open, under the link's lock alone. The
    /// form's deletion between that open's read and its save leaves no token on the deleted submission.
    /// </summary>
    [Fact]
    public async Task Resuming_a_link_while_its_form_is_deleted_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Toledo");
        using var anonymous = world.AnonymousClient();
        var submissionId = IdOf(await RedeemAsync(anonymous, formId, token), "submissionId");
        await ClearTokenAsync(world.Services, submissionId);
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(OpenTokenSave());
        var resuming = requests.Add(
            anonymous.PostAsJsonAsync(RedeemUrl(formId, token), new { }, admin.CancellationToken));
        await PausedAsync(pause, resuming);
        using var deleted = await admin.Client.DeleteAsync($"/api/forms/{formId}", admin.CancellationToken)
            .WaitAsync(_gateTimeout, admin.CancellationToken);

        // Act
        pause.Release();
        using var resumed = await resuming.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resumed.StatusCode);
        Assert.Null(await TokenOfAsync(world.Services, submissionId));
    }

    /// <summary>
    /// The form is disabled after the open passed its gates, while the open resolves the person's Submitter on a
    /// one-response form: the open reads the form again under the lock and does not open.
    /// </summary>
    [Fact]
    public async Task Opening_a_link_of_a_form_disabled_after_its_gate_check_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Lima", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(SubmitterRead());
        var opening = requests.Add(
            anonymous.PostAsJsonAsync(RedeemUrl(formId, token), new { }, admin.CancellationToken));
        await PausedAsync(pause, opening);
        using var disabled = await admin.Client.PatchAsJsonAsync(
            $"/api/forms/{formId}", new { isEnabled = false }, admin.CancellationToken);

        // Act
        pause.Release();
        using var opened = await opening.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, opened.StatusCode);
        Assert.Empty(await SubmissionsOfAsync(world.Services, formId));
    }

    /// <summary>The open's lookup of the person's Submitter: after its form check, before the lock.</summary>
    [GeneratedRegex("""FROM ["\[]Submitters["\]]""")]
    private static partial Regex SubmitterRead();

    /// <summary>
    /// The open takes over the person's on-behalf response, and that save loses a deadlock: the open answers busy,
    /// which a retry gets past, not "Link not found".
    /// </summary>
    [Fact]
    public async Task Opening_a_link_whose_take_over_loses_a_deadlock_is_unavailable()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Quito", limitOnePerUser: true);
        using var created = await OnBehalfAsync(admin, link.FormId, link.Identifier);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var anonymous = world.AnonymousClient();
        var deadlock = new PostgresException("deadlock detected", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected);
        using var fault = world.Services.GetRequiredService<AppCommandGate>().FailAt(OpenTokenSave(), deadlock);

        // Act
        using var opened = await anonymous.PostAsJsonAsync(
            RedeemUrl(link.FormId, link.Token), new { }, admin.CancellationToken);

        // Assert
        Assert.True(fault.Reached.IsCompleted, "The take-over never saved.");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, opened.StatusCode);
    }

    private static async Task<string?> TokenOfAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await app.Submissions.AsNoTracking()
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete, EndatixQueryFilterNames.Tenant])
            .Where(item => item.Id == submissionId)
            .Select(item => item.Token!.Value)
            .SingleAsync(TestContext.Current.CancellationToken);
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

    /// <summary>An open's save of a submission's token: after it read the form, before it commits.</summary>
    [GeneratedRegex("""UPDATE ["\[]Submissions["\]]""")]
    private static partial Regex OpenTokenSave();
}
