using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A test submission opened or started over while its form is deleted, disabled or republished, forced by pausing
/// one request at a statement. A test submission takes its form's lock like every other create, so the form ends
/// with no live submission whichever goes first, and a new one is built on the form's definition active at the
/// insert.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    /// <summary>The open does not queue behind the delete with a connection held: it is refused at once.</summary>
    [Fact]
    public async Task Opening_a_test_submission_while_its_form_is_being_deleted_is_unavailable_at_once()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Akron");
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(FormUpdate());
        var deleting = requests.Add(admin.Client.DeleteAsync($"/api/forms/{person.FormId}", admin.CancellationToken));
        await PausedAsync(pause, deleting);

        // Act
        var opening = requests.Add(OpenTestAsync(admin, person));
        var openWaited = await WaitedAsync(opening);
        pause.Release();
        using var deleted = await deleting.WaitAsync(_gateTimeout, admin.CancellationToken);
        using var opened = await opening.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Empty(await SubmissionsOfAsync(world.Services, person.FormId));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, opened.StatusCode);
        Assert.False(openWaited, "The open waited for the form's delete.");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_form_while_a_test_submission_is_being_opened_deletes_that_submission_too()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Dayton");
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(SubmissionInsertStatement());
        var opening = requests.Add(OpenTestAsync(admin, person));
        await PausedAsync(pause, opening);

        // Act
        var deleting = requests.Add(admin.Client.DeleteAsync($"/api/forms/{person.FormId}", admin.CancellationToken));
        var deleteWaited = await WaitedAsync(deleting);
        pause.Release();
        using var opened = await opening.WaitAsync(_gateTimeout, admin.CancellationToken);
        using var deleted = await deleting.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        var submissionId = IdOf(await admin.ReadAsync(opened), "submissionId");
        Assert.Equal(
            new Dictionary<long, bool> { [submissionId] = true },
            await SubmissionsOfAsync(world.Services, person.FormId));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(deleteWaited, "The delete did not wait for the open under way.");
    }

    /// <summary>
    /// The form is disabled after the open's first check and before it takes the form's lock: the open reads the
    /// form again under the lock and opens nothing.
    /// </summary>
    [Fact]
    public async Task Opening_a_test_submission_of_a_form_disabled_after_its_first_check_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Lima");
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(FormLockStatement());
        var opening = requests.Add(OpenTestAsync(admin, person));
        await PausedAsync(pause, opening);
        using var disabled = await admin.Client.PatchAsJsonAsync(
            $"/api/forms/{person.FormId}", new { isEnabled = false }, admin.CancellationToken);

        // Act
        pause.Release();
        using var opened = await opening.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, opened.StatusCode);
        Assert.Empty(await SubmissionsOfAsync(world.Services, person.FormId));
    }

    [Fact]
    public async Task Opening_a_test_submission_after_a_definition_is_published_meanwhile_uses_that_definition()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Xenia");
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(FormLockStatement());
        var opening = requests.Add(OpenTestAsync(admin, person));
        await PausedAsync(pause, opening);
        var definitionId = await PublishAsync(
            admin, person.FormId, """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q2"}]}]}""");

        // Act
        pause.Release();
        using var opened = await opening.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        var submissionId = IdOf(await admin.ReadAsync(opened), "submissionId");
        Assert.Equal(definitionId, IdOf(await SubmissionAsync(admin, person.FormId, submissionId), "formDefinitionId"));
    }

    /// <summary>
    /// The delete holds the form's lock and has not yet reached the submissions when the Start over arrives: the
    /// Start over is refused at once, keeps nothing, and the delete then deletes the old test submission.
    /// </summary>
    [Fact]
    public async Task Starting_over_while_its_form_is_being_deleted_is_unavailable_at_once_and_leaves_nothing_live()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Akron");
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        var openedId = IdOf(opened, "submissionId");
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(SubmissionsSoftDelete());
        var deleting = requests.Add(admin.Client.DeleteAsync($"/api/forms/{person.FormId}", admin.CancellationToken));
        await PausedAsync(pause, deleting);

        // Act
        var restarting = requests.Add(admin.PostAsync(RestartUrl(person.FormId, openedId), new { token = TokenOf(opened) }));
        var restartWaited = await WaitedAsync(restarting);
        pause.Release();
        using var deleted = await deleting.WaitAsync(_gateTimeout, admin.CancellationToken);
        using var restarted = await restarting.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, restarted.StatusCode);
        Assert.Equal(
            new Dictionary<long, bool> { [openedId] = true },
            await SubmissionsOfAsync(world.Services, person.FormId));
        Assert.False(restartWaited, "The Start over waited for the form's delete.");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
    }

    /// <summary>
    /// The delete commits after the Start over read the submission and before it took the form's lock: the Start
    /// over reads the form again under the lock and opens nothing.
    /// </summary>
    [Fact]
    public async Task Starting_over_after_its_form_is_deleted_meanwhile_is_not_found_and_leaves_nothing_live()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Toledo");
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        var openedId = IdOf(opened, "submissionId");
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(FormLockStatement());
        var restarting = requests.Add(admin.PostAsync(RestartUrl(person.FormId, openedId), new { token = TokenOf(opened) }));
        await PausedAsync(pause, restarting);
        using var deleted = await admin.Client.DeleteAsync($"/api/forms/{person.FormId}", admin.CancellationToken)
            .WaitAsync(_gateTimeout, admin.CancellationToken);

        // Act
        pause.Release();
        using var restarted = await restarting.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, restarted.StatusCode);
        Assert.Equal(
            new Dictionary<long, bool> { [openedId] = true },
            await SubmissionsOfAsync(world.Services, person.FormId));
    }

    [Fact]
    public async Task Deleting_a_form_while_a_test_submission_starts_over_deletes_the_new_one_too()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Canton");
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        var openedId = IdOf(opened, "submissionId");
        await using var requests = new StartedRequests();
        using var pause = world.Services.GetRequiredService<AppCommandGate>().PauseAt(SubmissionInsertStatement());
        var restarting = requests.Add(admin.PostAsync(RestartUrl(person.FormId, openedId), new { token = TokenOf(opened) }));
        await PausedAsync(pause, restarting);

        // Act
        var deleting = requests.Add(admin.Client.DeleteAsync($"/api/forms/{person.FormId}", admin.CancellationToken));
        var deleteWaited = await WaitedAsync(deleting);
        pause.Release();
        using var restarted = await restarting.WaitAsync(_gateTimeout, admin.CancellationToken);
        using var deleted = await deleting.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, restarted.StatusCode);
        var newId = IdOf(await admin.ReadAsync(restarted), "submissionId");
        Assert.Equal(
            new Dictionary<long, bool> { [openedId] = true, [newId] = true },
            await SubmissionsOfAsync(world.Services, person.FormId));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(deleteWaited, "The delete did not wait for the Start over under way.");
    }

    private static Task<HttpResponseMessage> OpenTestAsync(AudienceApi admin, IssuedLink person) =>
        admin.PostAsync(TestSubmissionsUrl(person.FormId), new { membershipId = MembershipIdOf(person) });

    private static async Task<bool> WaitedAsync(Task request) =>
        await Task.WhenAny(request, Task.Delay(SecondOpenProbe, TestContext.Current.CancellationToken)) != request;

    /// <summary>The first transaction lock the host's context asks for: PostgreSQL's shared advisory lock, or SQL Server's applock.</summary>
    [GeneratedRegex("pg_advisory_xact_lock_shared|sp_getapplock")]
    private static partial Regex FormLockStatement();

    /// <summary>The save of a new submission, under the form's lock once it is held.</summary>
    [GeneratedRegex("""INSERT INTO ["\[]Submissions["\]]""")]
    private static partial Regex SubmissionInsertStatement();

    /// <summary>
    /// The delete's first batch of soft-deleted submissions: under the form's lock, before it locks any submission.
    /// PostgreSQL writes <c>UPDATE "Submissions" AS s</c>; SQL Server <c>UPDATE [s] ... FROM [Submissions] AS [s]</c>.
    /// </summary>
    [GeneratedRegex("""\AUPDATE\b.*?["\[]Submissions["\]]""", RegexOptions.Singleline)]
    private static partial Regex SubmissionsSoftDelete();
}
