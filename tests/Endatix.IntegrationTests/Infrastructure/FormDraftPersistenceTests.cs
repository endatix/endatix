using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Infrastructure.Data.Repositories;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Persistence.PostgreSql.Locking;
using Endatix.Persistence.SqlServer.Locking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.IntegrationTests;

[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
public sealed class FormDraftPersistenceTests(DbIntegrationFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly DateTime _at = new(2024, 3, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunAsync_TenantScope_LeavesAnotherTenantsEligibleTailUntouched()
    {
        // Arrange
        await using var first = await OpenAsync();
        await using var second = await OpenAsync();
        var local = await SaveJournalAsync(first);
        var neighbour = await SaveJournalAsync(second);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(_at.AddDays(1)));
        var formLock = new FormSubmissionsLock(first,
            first.Database.IsNpgsql() ? new PostgreSqlTransactionLock() : new SqlServerTransactionLock());
        var scan = new IdleDraftCheckpoints(new IdleDraftStore(first, Repository(first), formLock), clock);

        // Act
        await scan.RunAsync(Cancellation);

        // Assert
        var stored = await first.FormDraftOperations.SingleAsync(row => row.Reason == "idle", Cancellation);
        stored.TenantId.Should().Be(first.GetTenantId());
        stored.FormId.Should().Be(local.FormId);
        stored.EditorSessionId.Should().BeNull();
        stored.ContentHash.Should().Be(local.ContentHash);
        (await second.FormDraftOperations.CountAsync(Cancellation)).Should().Be(2);
        (await second.FormDrafts.AsNoTracking().SingleAsync(Cancellation)).Version.Should().Be(neighbour.Version);
    }

    private static FormDraftRepository Repository(AppDbContext db) => new(db, new UniqueConstraintViolationChecker());

    private static async Task<FormDraft> SaveJournalAsync(AppDbContext db)
    {
        var head = FormDraft.For(await CreateFormAsync(db), "{}");
        var batch = new DraftJournalBatch(1, "session", ["{}"], """{"n":1}""", new string('f', 64));
        await Repository(db).SaveOrConcedeAsync(head, head.Append(batch, DraftWrite.System(_at), 0).Value, Cancellation);
        db.ChangeTracker.Clear();
        return head;
    }

    [Fact]
    public async Task SaveChanges_ExistingSettings_PreservesWriterClock()
    {
        await using var db = await OpenAsync();
        var settings = FormSettings.For(await CreateFormAsync(db));
        settings.Apply(new() { Pdf = new() { UsesFormTheme = true } }, 0, _at);
        db.FormSettings.Add(settings);
        await db.SaveChangesAsync(Cancellation);

        settings.Apply(new() { Pdf = new() }, 1, _at);
        await db.SaveChangesAsync(Cancellation);
        await db.Entry(settings).ReloadAsync(Cancellation);

        settings.Version.Should().Be(2);
        settings.ModifiedAt.Should().Be(_at);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task SaveChanges_ExistingDraft_PreservesWriterClock(int daysLater)
    {
        // Arrange
        await using var db = await OpenAsync();
        var form = await CreateFormAsync(db);
        var draft = FormDraft.For(form, "{}");
        var repo = new FormDraftRepository(db, new UniqueConstraintViolationChecker());
        await repo.SaveOrConcedeAsync(draft, draft.Replace("""{"n":1}""", DraftWrite.System(_at)), Cancellation);
        db.ChangeTracker.Clear();
        var existing = (await repo.FindHeadAsync(form.Id, Cancellation))!;
        var updatedAt = _at.AddDays(daysLater);

        // Act
        var journal = new DraftJournalBatch(1, "editor", ["{}"], "{}", new string('f', 64));
        var saved = await repo.SaveOrConcedeAsync(existing, existing.Append(journal, DraftWrite.System(updatedAt), 2).Value, Cancellation);
        db.ChangeTracker.Clear();

        // Assert
        var stored = await repo.FindHeadAsync(form.Id, Cancellation);
        saved.Should().BeTrue();
        stored!.Version.Should().Be(3);
        stored!.ModifiedAt.Should().Be(updatedAt);
        var operation = await db.FormDraftOperations.SingleAsync(row => row.Version == stored.Version, Cancellation);
        operation.CreatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public async Task SaveChanges_OrdinaryFormUpdate_StillSetsModificationTime()
    {
        // Arrange
        await using var db = await OpenAsync();
        var form = await CreateFormAsync(db);
        form.ModifiedAt.Should().BeNull();

        // Act
        var before = DateTime.UtcNow.AddMilliseconds(-1);
        form.Name = "Renamed";
        await db.SaveChangesAsync(Cancellation);
        await db.Entry(form).ReloadAsync(Cancellation);

        // Assert
        form.ModifiedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task DeleteForm_HistorySurvivesSoftDelete_AndCascadesOnPhysicalRemoval()
    {
        // Arrange
        await using var db = await OpenAsync();
        var form = await CreateFormAsync(db);
        var draft = FormDraft.For(form, "{}");
        var rows = draft.Replace("""{"n":1}""", DraftWrite.System(_at));
        await new FormDraftRepository(db, new UniqueConstraintViolationChecker()).SaveOrConcedeAsync(draft, rows, Cancellation);

        // Act: the form deletion handler marks the aggregate as deleted.
        form.Delete();
        await db.SaveChangesAsync(Cancellation);

        // Assert: physical parent removal uses the database cascade, even with the operations tracked.
        (await db.Forms.AnyAsync(Cancellation)).Should().BeFalse();
        (await db.FormDraftOperations.CountAsync(Cancellation)).Should().Be(2);
        await db.Forms.IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete])
            .Where(item => item.Id == form.Id).ExecuteDeleteAsync(Cancellation);
        (await db.FormDrafts.AnyAsync(Cancellation)).Should().BeFalse();
        (await db.FormDraftOperations.AnyAsync(Cancellation)).Should().BeFalse();
    }

    [Fact]
    public async Task LatestSnapshot_IndexCanServeTheBoundedVersionLookup()
    {
        // Arrange: enough edit history for an index read to be cheaper than a scan of a few pages.
        using var capture = new SnapshotLookupPlan();
        await using var db = await OpenAsync(capture);
        var form = await CreateFormAsync(db);
        var head = FormDraft.For(form, "{}");
        var batch = new DraftJournalBatch(1, "editor", Enumerable.Repeat("{}", 200).ToArray(), "{}", new string('a', 64));
        var repo = new FormDraftRepository(db, new UniqueConstraintViolationChecker());
        for (var batchNo = 0; batchNo < 20; batchNo++)
        {
            var rows = head.Append(batch, DraftWrite.System(_at), head.Version).Value;
            await repo.SaveOrConcedeAsync(head, rows, Cancellation);
        }

        // Act
        await db.Database.OpenConnectionAsync(Cancellation);
        capture.CaptureNextRead = true;
        var version = await repo.LatestSnapshotVersionAsync(form.Id, 200, Cancellation);

        // Assert: inspect the command with the tenant filter and the same bound parameters.
        version.Should().Be(1);
        var plan = await capture.ExplainAsync(db.Database, Cancellation);
        plan.Should().Contain("IX_FormDraftOperations_FormId_Version");
    }

    [Fact]
    public async Task Remove_TrackedParentWithHistory_RefusesClientCascade()
    {
        await using var db = await OpenAsync();
        var draft = FormDraft.For(await CreateFormAsync(db), "{}");
        await new FormDraftRepository(db, new UniqueConstraintViolationChecker())
            .SaveOrConcedeAsync(draft, draft.Replace("""{"n":1}""", DraftWrite.System(_at)), Cancellation);

        db.Remove(draft);

        var save = () => db.SaveChangesAsync(Cancellation);
        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*insert-only*");
    }

    private async Task<AppDbContext> OpenAsync(params IInterceptor[] interceptors)
    {
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        DbContextOptionsBuilder<AppDbContext> options = new();
        IntegrationAppDbContextFactory.ConfigureOptions(options, fixture.Database.ConnectionString, fixture.Database.Provider);
        options.AddInterceptors(interceptors);
        return new(options.Options, new IntegrationTenantContext(tenantId), new OutboxIntegrationEventDispatcher());
    }

    private static async Task<Form> CreateFormAsync(AppDbContext db)
    {
        var form = Form.Create(new FormCreateArgs(TenantId: db.GetTenantId(), Name: "Draft owner"));
        db.Forms.Add(form);
        await db.SaveChangesAsync(Cancellation);
        return form;
    }
}
