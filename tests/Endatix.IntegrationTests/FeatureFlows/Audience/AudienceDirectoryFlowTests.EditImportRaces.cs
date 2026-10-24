using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data.Locking;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// An edit of a participant's values and a CSV import of their form (endatix#1384), forced instead of hoped for: the edit
/// takes its participant's stripe of the form's people lock, which the import holds shared, so it never waits on the
/// import's uncommitted value, which only the command timeout would bound.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    /// <summary>The wait of a request that should find its lock held and answer 503.</summary>
    private static readonly TimeSpan BusyWait = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How soon a request with <see cref="BusyWait"/> answers, with room for a slow run; well under the 30 s command
    /// timeout that bounds a wait on a row or an index.
    /// </summary>
    private static readonly TimeSpan BusyAnswer = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Editing_a_participant_while_an_import_holds_their_uncommitted_value_is_unavailable_within_the_wait()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var gate = CommandGate.AtCommit();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        await using var paused = new PausedRace<Result<ImportResultDto>>(
            gate,
            ImportHandlerWith(scope.ServiceProvider, gated, Waiting(world, RaceWait))
                .Handle(CityImport(world, formId, "ada@example.com"), admin.CancellationToken));
        await paused.ReachedGateAsync();
        await using var second = world.Services.CreateAsyncScope();
        var secondDb = second.ServiceProvider.GetRequiredService<IAudienceDbContext>();

        // Act
        var edit = paused.Alongside(UpdateHandlerWith(second.ServiceProvider, secondDb, Waiting(world, BusyWait)).Handle(
            new UpdatePersonCommand(world.Tenants[0].Id, formId, ada.MembershipId, Values(city, "Plovdiv")),
            admin.CancellationToken));
        var answeredInTime = await Task.WhenAny(edit, Task.Delay(BusyAnswer, admin.CancellationToken)) == edit;
        var imported = await paused.ReleaseAsync();
        var edited = await edit.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.True(answeredInTime, "The edit waited on the import's uncommitted value past its lock wait.");
        Assert.Equal(ResultStatus.Unavailable, edited.Status);
        Assert.Equal([MembershipLock.FormBusy], edited.Errors);
        Assert.Equal(1, imported.Value.UpdatedCount);
        Assert.Equal(new Dictionary<long, string> { [city] = "Sofia" }, await LiveCellsAsync(world.Services, formId));
    }

    [Fact]
    public async Task Importing_a_participant_while_an_edit_writes_their_value_waits_and_overwrites_it()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var gate = CommandGate.AtCellInsert();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        await using var paused = new PausedRace<Result<PersonDto>>(
            gate,
            UpdateHandlerWith(scope.ServiceProvider, gated, Waiting(world, RaceWait)).Handle(
                new UpdatePersonCommand(world.Tenants[0].Id, formId, ada.MembershipId, Values(city, "Plovdiv")),
                admin.CancellationToken));
        await paused.ReachedGateAsync();
        await using var second = world.Services.CreateAsyncScope();
        var secondDb = second.ServiceProvider.GetRequiredService<IAudienceDbContext>();

        // Act
        var import = paused.Alongside(ImportHandlerWith(second.ServiceProvider, secondDb, Waiting(world, RaceWait))
            .Handle(CityImport(world, formId, "ada@example.com"), admin.CancellationToken));
        var importWaited = await Task.WhenAny(import, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != import;
        var edited = await paused.ReleaseAsync();
        var imported = await import.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.True(importWaited, "The import did not wait for the edit's lock.");
        Assert.True(edited.IsSuccess, string.Join("; ", edited.Errors));
        Assert.Equal(1, imported.Value.UpdatedCount);
        Assert.Equal(new Dictionary<long, string> { [city] = "Sofia" }, await LiveCellsAsync(world.Services, formId));
    }

    /// <summary>
    /// A writer that skips the locks (a pod on the previous version during a deploy) commits the participant's value
    /// while the edit is paused at its insert. The unique index refuses the edit's insert, and a second locked pass
    /// overwrites the value.
    /// </summary>
    [Fact]
    public async Task Editing_a_participant_whose_value_another_writer_adds_first_overwrites_it_in_a_second_pass()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var gate = CommandGate.AtCellInsert();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        await using var paused = new PausedRace<Result<PersonDto>>(
            gate,
            UpdateHandlerWith(scope.ServiceProvider, gated, Waiting(world, RaceWait)).Handle(
                new UpdatePersonCommand(world.Tenants[0].Id, formId, ada.MembershipId, Values(city, "Plovdiv")),
                admin.CancellationToken));
        await paused.ReachedGateAsync();
        await AddCellsWithoutTheLockAsync(world, ada.MembershipId, Values(city, "Sofia"));

        // Act
        var edited = await paused.ReleaseAsync();

        // Assert
        Assert.True(edited.IsSuccess, string.Join("; ", edited.Errors));
        Assert.Equal(new Dictionary<long, string> { [city] = "Plovdiv" }, await LiveCellsAsync(world.Services, formId));
    }

    /// <summary>Inserts the participant's cells and commits, without any audience lock.</summary>
    private static async Task AddCellsWithoutTheLockAsync(
        IntegrationTestWorld world,
        long membershipId,
        Dictionary<long, string> values)
    {
        await using var scope = world.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        db.PropertyValues.AddRange(values.Select(cell => new PropertyValue(
            new PropertyValueCreateArgs(world.Tenants[0].Id, membershipId, cell.Key, cell.Value))));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
