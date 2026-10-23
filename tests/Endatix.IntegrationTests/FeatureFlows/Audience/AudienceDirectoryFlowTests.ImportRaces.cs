using System.Net;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A CSV import that updates a participant's values racing their removal (endatix#1373), forced in both orders: the
/// removed participant never keeps a live value the import wrote.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    /// <summary>
    /// The import has claimed the participant's membership and is paused at its cell insert. The removal waits for the
    /// import to commit, then removes the cell it wrote.
    /// </summary>
    [Fact]
    public async Task Removing_a_participant_while_an_import_writes_their_value_waits_and_leaves_no_live_value()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var gate = SubmissionCommandGate.AtCellInsert();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        var import = ImportHandlerWith(scope.ServiceProvider, gated).Handle(
            CityImport(world.Tenants[0].Id, formId, ada),
            admin.CancellationToken);
        await using var paused = new PausedRace<Result<ImportResultDto>>(gate, import);
        await paused.ReachedGateAsync();

        // Act
        var removal = paused.Alongside(admin.Client.DeleteAsync(PersonUrl(formId, ada), admin.CancellationToken));
        var removalWaited = await Task.WhenAny(removal, Task.Delay(SecondOpenProbe, admin.CancellationToken)) != removal;
        var imported = await paused.ReleaseAsync();
        using var removed = await removal.WaitAsync(_gateTimeout, admin.CancellationToken);

        // Assert
        Assert.True(removalWaited, "The removal did not wait for the import's claim on the membership.");
        Assert.True(imported.IsSuccess);
        Assert.Equal(1, imported.Value.UpdatedCount);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(0, await LiveCellsAsync(world.Services, ada.MembershipId));
    }

    /// <summary>
    /// The participant is removed after the import read them as on the form, before it saves. The import's claim finds
    /// the membership gone, so it rolls back and its second pass adds the person to the form again.
    /// </summary>
    [Fact]
    public async Task Importing_a_value_for_a_participant_removed_after_the_import_read_them_adds_them_again()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var gate = SubmissionCommandGate.AtCellRead();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        var import = ImportHandlerWith(scope.ServiceProvider, gated).Handle(
            CityImport(world.Tenants[0].Id, formId, ada),
            admin.CancellationToken);
        await using var paused = new PausedRace<Result<ImportResultDto>>(gate, import);
        await paused.ReachedGateAsync();
        using var removed = await admin.Client.DeleteAsync(PersonUrl(formId, ada), admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        // Act
        var imported = await paused.ReleaseAsync();

        // Assert
        Assert.True(imported.IsSuccess);
        Assert.Equal(1, imported.Value.CreatedCount);
        Assert.Equal(0, imported.Value.UpdatedCount);
        Assert.Equal(0, await LiveCellsAsync(world.Services, ada.MembershipId));
    }

    /// <summary>A one-row file that gives <paramref name="participant"/> a City they do not have yet.</summary>
    private static ImportCsvCommand CityImport(long tenantId, long formId, Participant participant) =>
        new(
            tenantId,
            formId,
            $"email,City\n{participant.Identifier},Sofia\n",
            "email",
            "people.csv",
            new Dictionary<string, string> { ["city"] = "City" });

    /// <summary>The host's handler, with every audience read and write going through <paramref name="db"/>.</summary>
    private static ImportCsvHandler ImportHandlerWith(IServiceProvider services, IAudienceDbContext db) =>
        new(
            db,
            services.GetRequiredService<IRepository<Form>>(),
            services.GetRequiredService<ImportWriteSupport>(),
            services.GetRequiredService<MatchKeyLock>());
}
