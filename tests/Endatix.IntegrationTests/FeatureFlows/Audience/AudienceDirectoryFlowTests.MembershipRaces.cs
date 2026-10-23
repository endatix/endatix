using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data.Locking;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A write of a participant's values (an edit, or a CSV import of their form) racing their removal (endatix#1361),
/// forced instead of hoped for: the writer and the removal take the participant's or the form's lock, so the removed
/// participant never keeps a live value. The second request runs on a lock that waits <see cref="RaceWait"/>, so a slow
/// run never turns its wait into a 503.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    /// <summary>Longer than any paused race takes, shorter than the command timeout.</summary>
    private static readonly TimeSpan RaceWait = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task Removing_a_participant_while_an_edit_writes_their_value_waits_and_leaves_no_live_value()
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
                new UpdatePersonCommand(world.Tenants[0].Id, formId, ada.MembershipId, Values(city, "Sofia")),
                admin.CancellationToken));
        await paused.ReachedGateAsync();
        await using var second = world.Services.CreateAsyncScope();

        // Act
        var removal = paused.Alongside(DeleteHandlerWith(second.ServiceProvider, Waiting(world, RaceWait))
            .Handle(Removal(world, formId, ada), admin.CancellationToken));
        var removalWaited = await Task.WhenAny(removal, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != removal;
        var edited = await paused.ReleaseAsync();
        var removed = await removal.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.True(removalWaited, "The removal did not wait for the edit's lock.");
        Assert.True(edited.IsSuccess);
        Assert.True(removed.IsSuccess);
        Assert.Equal(0, await LiveCellCountAsync(world.Services, ada.MembershipId));
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
        var gate = CommandGate.AtMembershipRemoval();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        await using var paused = new PausedRace<Result<string>>(
            gate,
            DeleteHandlerWith(scope.ServiceProvider, Waiting(world, RaceWait), gated)
                .Handle(Removal(world, formId, ada), admin.CancellationToken));
        await paused.ReachedGateAsync();
        await using var second = world.Services.CreateAsyncScope();
        var secondDb = second.ServiceProvider.GetRequiredService<IAudienceDbContext>();

        // Act
        var edit = paused.Alongside(UpdateHandlerWith(second.ServiceProvider, secondDb, Waiting(world, RaceWait)).Handle(
            new UpdatePersonCommand(world.Tenants[0].Id, formId, ada.MembershipId, Values(city, "Sofia")),
            admin.CancellationToken));
        var editWaited = await Task.WhenAny(edit, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != edit;
        var removed = await paused.ReleaseAsync();
        var edited = await edit.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.True(editWaited, "The edit did not wait for the removal's lock.");
        Assert.True(removed.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, edited.Status);
        Assert.Equal(0, await LiveCellCountAsync(world.Services, ada.MembershipId));
    }

    [Fact]
    public async Task Removing_a_participant_while_an_import_writes_their_value_waits_and_leaves_no_live_value()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var gate = CommandGate.AtCellInsert();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        await using var paused = new PausedRace<Result<ImportResultDto>>(
            gate,
            ImportHandlerWith(scope.ServiceProvider, gated, Waiting(world, RaceWait))
                .Handle(CityImport(world, formId, "ada@example.com"), admin.CancellationToken));
        await paused.ReachedGateAsync();
        await using var second = world.Services.CreateAsyncScope();

        // Act
        var removal = paused.Alongside(DeleteHandlerWith(second.ServiceProvider, Waiting(world, RaceWait))
            .Handle(Removal(world, formId, ada), admin.CancellationToken));
        var removalWaited = await Task.WhenAny(removal, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != removal;
        var imported = await paused.ReleaseAsync();
        var removed = await removal.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(0, await LiveCellCountAsync(world.Services, ada.MembershipId));
        Assert.True(removalWaited, "The removal did not wait for the import's lock.");
        Assert.Equal(1, imported.Value.UpdatedCount);
        Assert.True(removed.IsSuccess);
    }

    [Fact]
    public async Task Importing_a_participant_while_they_are_removed_waits_and_adds_them_again()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var gate = CommandGate.AtMembershipRemoval();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        await using var paused = new PausedRace<Result<string>>(
            gate,
            DeleteHandlerWith(scope.ServiceProvider, Waiting(world, RaceWait), gated)
                .Handle(Removal(world, formId, ada), admin.CancellationToken));
        await paused.ReachedGateAsync();
        await using var second = world.Services.CreateAsyncScope();
        var secondDb = second.ServiceProvider.GetRequiredService<IAudienceDbContext>();

        // Act
        var import = paused.Alongside(ImportHandlerWith(second.ServiceProvider, secondDb, Waiting(world, RaceWait))
            .Handle(CityImport(world, formId, "ada@example.com"), admin.CancellationToken));
        var importWaited = await Task.WhenAny(import, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != import;
        var removed = await paused.ReleaseAsync();
        var imported = await import.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.Equal(0, await LiveCellCountAsync(world.Services, ada.MembershipId));
        Assert.True(importWaited, "The import did not wait for the removal's lock.");
        Assert.True(removed.IsSuccess);
        Assert.Equal(1, imported.Value.CreatedCount);
    }

    [Fact]
    public async Task Writers_of_a_participant_held_past_the_wait_are_unavailable()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        await using var scope = world.Services.CreateAsyncScope();
        var holder = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var held = await scope.ServiceProvider.GetRequiredService<MembershipLock>()
            .BeginRemovalAsync(holder, new(formId, ada.MembershipId), admin.CancellationToken);
        await using var heldTransaction = held.Value;
        await using var second = world.Services.CreateAsyncScope();
        var secondDb = second.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var brief = Waiting(world, TimeSpan.FromMilliseconds(50));

        // Act
        var edited = await UpdateHandlerWith(second.ServiceProvider, secondDb, brief).Handle(
            new UpdatePersonCommand(world.Tenants[0].Id, formId, ada.MembershipId, Values(city, "Sofia")),
            admin.CancellationToken);
        var removed = await DeleteHandlerWith(second.ServiceProvider, brief)
            .Handle(Removal(world, formId, ada), admin.CancellationToken);
        var imported = await ImportHandlerWith(second.ServiceProvider, secondDb, brief)
            .Handle(CityImport(world, formId, "ada@example.com"), admin.CancellationToken);

        // Assert
        Assert.Equal(ResultStatus.Unavailable, edited.Status);
        Assert.Equal(ResultStatus.Unavailable, removed.Status);
        Assert.Equal(ResultStatus.Unavailable, imported.Status);
    }

    private static Dictionary<long, string> Values(long propertyId, string value) => new() { [propertyId] = value };

    private static ImportCsvCommand CityImport(IntegrationTestWorld world, long formId, string identifier) =>
        new(
            world.Tenants[0].Id,
            formId,
            $"email,City\n{identifier},Sofia\n",
            "email",
            "people.csv",
            new Dictionary<string, string> { ["city"] = "City" });

    /// <summary>The participant lock with its wait set by the test: long for a racing request, short for a held one.</summary>
    private static MembershipLock Waiting(IntegrationTestWorld world, TimeSpan wait) =>
        new(world.Services.GetRequiredService<ITransactionLock>()) { Wait = wait };

    /// <summary>The host's handler, with every audience read and write going through <paramref name="db"/>.</summary>
    private static UpdatePersonHandler UpdateHandlerWith(
        IServiceProvider services,
        IAudienceDbContext db,
        MembershipLock membershipLock) =>
        new(
            db,
            services.GetRequiredService<IRepository<Form>>(),
            new LockedValuesWriter(db, membershipLock, services.GetRequiredService<IUniqueConstraintViolationChecker>()),
            services.GetRequiredService<ParticipantReader>());

    private static ImportCsvHandler ImportHandlerWith(
        IServiceProvider services,
        IAudienceDbContext db,
        MembershipLock membershipLock) =>
        new(
            db,
            services.GetRequiredService<IRepository<Form>>(),
            new ImportWriteSupport(
                services.GetRequiredService<IValueNormalizer>(),
                services.GetRequiredService<IUniqueConstraintViolationChecker>(),
                membershipLock),
            services.GetRequiredService<MatchKeyLock>());

    private static DeletePersonHandler DeleteHandlerWith(
        IServiceProvider services,
        MembershipLock membershipLock,
        IAudienceDbContext? db = null) =>
        new(
            db ?? services.GetRequiredService<IAudienceDbContext>(),
            services.GetRequiredService<IRepository<Form>>(),
            membershipLock);

    private static DeletePersonCommand Removal(IntegrationTestWorld world, long formId, Participant participant) =>
        new(world.Tenants[0].Id, formId, participant.MembershipId);

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
    private static async Task<int> LiveCellCountAsync(IServiceProvider services, long membershipId)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAudienceDbContext>().PropertyValues
            .CountAsync(cell => cell.MembershipId == membershipId, TestContext.Current.CancellationToken);
    }
}
