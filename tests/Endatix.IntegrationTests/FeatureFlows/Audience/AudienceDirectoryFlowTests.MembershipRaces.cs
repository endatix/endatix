using System.Net;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// An edit of a participant's values racing their removal (endatix#1361), forced instead of hoped for: both take the
/// participant's lock, so the removed participant never keeps a live value.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Removing_a_participant_while_an_edit_writes_their_value_waits_and_leaves_no_live_value()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var gate = SubmissionCommandGate.AtCellInsert();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        var edit = UpdateHandlerWith(scope.ServiceProvider, gated).Handle(
            new UpdatePersonCommand(world.Tenants[0].Id, formId, ada.MembershipId, Values(city, "Sofia")),
            admin.CancellationToken);
        await using var paused = new PausedRace<Result<PersonDto>>(gate, edit);
        await paused.ReachedGateAsync();

        // Act
        var removal = paused.Alongside(admin.Client.DeleteAsync(PersonUrl(formId, ada), admin.CancellationToken));
        var removalWaited = await Task.WhenAny(removal, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != removal;
        var edited = await paused.ReleaseAsync();
        using var removed = await removal.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.True(removalWaited, "The removal did not wait for the edit's lock.");
        Assert.True(edited.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(0, await LiveCellsAsync(world.Services, ada.MembershipId));
    }

    [Fact]
    public async Task Editing_a_participant_while_they_are_removed_waits_and_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var gate = SubmissionCommandGate.AtMembershipRemoval();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        var removal = DeleteHandlerWith(scope.ServiceProvider, gated).Handle(
            new DeletePersonCommand(world.Tenants[0].Id, formId, ada.MembershipId),
            admin.CancellationToken);
        await using var paused = new PausedRace<Result<string>>(gate, removal);
        await paused.ReachedGateAsync();

        // Act
        var edit = paused.Alongside(admin.PutValueAsync(PersonUrl(formId, ada), city, "Sofia"));
        var editWaited = await Task.WhenAny(edit, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != edit;
        var removed = await paused.ReleaseAsync();
        using var edited = await edit.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.True(editWaited, "The edit did not wait for the removal's lock.");
        Assert.True(removed.IsSuccess);
        Assert.Equal(HttpStatusCode.NotFound, edited.StatusCode);
        Assert.Equal(0, await LiveCellsAsync(world.Services, ada.MembershipId));
    }

    [Fact]
    public async Task Editing_a_participant_held_past_the_wait_is_unavailable()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        await using var scope = world.Services.CreateAsyncScope();
        var holder = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        await using var held = await scope.ServiceProvider.GetRequiredService<MembershipLock>()
            .BeginAsync(holder, ada.MembershipId, admin.CancellationToken);

        // Act
        using var edited = await admin.PutValueAsync(PersonUrl(formId, ada), city, "Sofia");
        using var removed = await admin.Client.DeleteAsync(PersonUrl(formId, ada), admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, edited.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, removed.StatusCode);
    }

    private static string PersonUrl(long formId, Participant participant) =>
        $"/api/forms/{formId}/audience/people/{participant.MembershipId}";

    private static Dictionary<long, string> Values(long propertyId, string value) => new() { [propertyId] = value };

    /// <summary>The host's handler, with every audience read and write going through <paramref name="db"/>.</summary>
    private static UpdatePersonHandler UpdateHandlerWith(IServiceProvider services, IAudienceDbContext db) =>
        new(
            db,
            services.GetRequiredService<IRepository<Form>>(),
            new LockedValuesWriter(
                db,
                services.GetRequiredService<MembershipLock>(),
                services.GetRequiredService<IUniqueConstraintViolationChecker>()),
            services.GetRequiredService<ParticipantReader>());

    private static DeletePersonHandler DeleteHandlerWith(IServiceProvider services, IAudienceDbContext db) =>
        new(db, services.GetRequiredService<IRepository<Form>>(), services.GetRequiredService<MembershipLock>());

    /// <summary>The host's audience context for the run's provider, with <paramref name="gate"/> on its commands.</summary>
    private static AudienceDbContextBase GatedAudienceDbContext(IServiceProvider services, IInterceptor gate)
    {
        var tenant = services.GetRequiredService<ITenantContext>();
        return services.GetRequiredService<IAudienceDbContext>() switch
        {
            AudiencePostgreSqlDbContext => new AudiencePostgreSqlDbContext(
                Gated(services.GetRequiredService<DbContextOptions<AudiencePostgreSqlDbContext>>(), gate), tenant),
            _ => new AudienceSqlServerDbContext(
                Gated(services.GetRequiredService<DbContextOptions<AudienceSqlServerDbContext>>(), gate), tenant),
        };
    }

    private static DbContextOptions<T> Gated<T>(DbContextOptions<T> options, IInterceptor gate)
        where T : DbContext =>
        new DbContextOptionsBuilder<T>(options).AddInterceptors(gate).Options;

    /// <summary>Value cells of the membership that are not soft-deleted.</summary>
    private static async Task<int> LiveCellsAsync(IServiceProvider services, long membershipId)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAudienceDbContext>().PropertyValues
            .CountAsync(cell => cell.MembershipId == membershipId, TestContext.Current.CancellationToken);
    }

    /// <summary>A handler paused at its gate, and the requests made alongside it; disposing drains them all.</summary>
    private sealed class PausedRace<T>(SubmissionCommandGate gate, Task<T> paused) : IAsyncDisposable
    {
        private readonly List<Task> _companions = [];

        public async Task ReachedGateAsync()
        {
            await Task.WhenAny(gate.Reached, paused).WaitAsync(_gateTimeout, TestContext.Current.CancellationToken);
            Assert.True(gate.Reached.IsCompleted, "The paused handler never reached its gate.");
        }

        public Task<T> ReleaseAsync()
        {
            gate.Release();
            return paused.WaitAsync(_gateTimeout, TestContext.Current.CancellationToken);
        }

        public Task<TCompanion> Alongside<TCompanion>(Task<TCompanion> companion)
        {
            _companions.Add(companion);
            return companion;
        }

        public async ValueTask DisposeAsync()
        {
            gate.Release();
            await ((Task)paused).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            foreach (var companion in _companions)
            {
                await companion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }
}
