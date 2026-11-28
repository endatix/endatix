using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Repositories;
using Endatix.Infrastructure.Features.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
public sealed class FormDraftPersistenceTests(EndatixIntegrationWebHostFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly DateTime _at = new(2024, 3, 2, 12, 0, 0, DateTimeKind.Utc);

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
        await repo.SaveOrConcedeAsync(existing, existing.Replace("{}", DraftWrite.System(updatedAt)), Cancellation);
        db.ChangeTracker.Clear();

        // Assert
        var stored = await repo.FindHeadAsync(form.Id, Cancellation);
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
        // Arrange: one snapshot followed by a full tail of uncheckpointed edits.
        await using var db = await OpenAsync();
        var form = await CreateFormAsync(db);
        var head = FormDraft.For(form, "{}");
        var batch = new DraftJournalBatch(1, "editor", Enumerable.Repeat("{}", 199).ToArray(), "{}", new string('a', 64));
        var repo = new FormDraftRepository(db, new UniqueConstraintViolationChecker());
        await repo.SaveOrConcedeAsync(head, head.Append(batch, DraftWrite.System(_at), 0).Value, Cancellation);

        // Act
        var version = await repo.LatestSnapshotVersionAsync(form.Id, 200, Cancellation);

        // Assert: ask each engine to plan the lookup against its filtered index. This tests eligibility,
        // not a cost estimate, which would depend on the size and statistics of the test database.
        version.Should().Be(1);
        await db.Database.OpenConnectionAsync(Cancellation);
        if (db.Database.IsSqlServer())
        {
            var indexed = await db.Database.SqlQuery<long>($"""
                SELECT MAX([Version]) AS [Value] FROM [FormDraftOperations] WITH (INDEX(IX_FormDraftOperations_FormId_Version))
                WHERE [FormId] = {form.Id} AND [Version] <= {200L} AND [Kind] = N'snapshot'
                """).SingleAsync(Cancellation);
            indexed.Should().Be(version);
        }
        else
        {
            await using var transaction = await db.Database.BeginTransactionAsync(Cancellation);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off", Cancellation);
            await db.Database.ExecuteSqlRawAsync("ANALYZE \"FormDraftOperations\"", Cancellation);
            var plan = await db.Database.SqlQuery<string>($"""
                EXPLAIN SELECT MAX("Version") FROM "FormDraftOperations"
                WHERE "FormId" = {form.Id} AND "Version" <= {200L} AND "Kind" = 'snapshot'
                """).ToListAsync(Cancellation);
            plan.Should().Contain(line => line.Contains("IX_FormDraftOperations_FormId_Version"));
        }
    }

    private async Task<AppDbContext> OpenAsync()
    {
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        DbContextOptionsBuilder<AppDbContext> options = new();
        IntegrationAppDbContextFactory.ConfigureOptions(options, fixture.Database.ConnectionString, fixture.Database.Provider);
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
