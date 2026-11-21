using Endatix.Core.Branding;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Publishing;
using Endatix.Core.UseCases.TenantBranding.Rollback;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

/// <summary>
/// A tenant's branding against the real database: the JSON columns (jsonb on PostgreSQL) keep publish idempotent after
/// a reload, and the revision token and the key turn a lost race into a conflict.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class TenantBrandingPersistenceTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string BLUE = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"}}},"assets":{"icon":"t/{tenant}/brand/icon.png"}}""";
    private const string GREEN = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#16a34a"}}}}""";
    private const string PURPLE = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#8a2be2"}}}}""";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Publishing_the_stored_value_again_after_a_reload_changes_nothing()
    {
        // Arrange
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        var blue = BLUE.Replace("{tenant}", tenantId.ToString(), StringComparison.Ordinal);
        await PublishAsync(tenantId, blue, 0);
        await PublishAsync(tenantId, GREEN, 1);

        // Act: a retry of the second publish, with the revision read before it
        var retry = await PublishAsync(tenantId, GREEN, 1);

        // Assert
        retry.IsSuccess.Should().BeTrue();
        var stored = await ReadAsync(tenantId);
        stored.Revision.Should().Be(2);
        stored.PublishedJson.Should().Contain("#16a34a");
        stored.PreviousJson.Should().Contain("#0052d1").And.Contain($"t/{tenantId}/brand/icon.png");
    }

    [Fact]
    public async Task Rolling_back_twice_returns_to_the_start()
    {
        // Arrange
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        await PublishAsync(tenantId, GREEN, 0);
        await PublishAsync(tenantId, PURPLE, 1);

        // Act
        var first = await RollbackAsync(tenantId, 2);
        var afterFirst = await ReadAsync(tenantId);
        var second = await RollbackAsync(tenantId, 3);

        // Assert
        first.IsSuccess.Should().BeTrue();
        afterFirst.PublishedJson.Should().Contain("#16a34a");
        second.IsSuccess.Should().BeTrue();
        var stored = await ReadAsync(tenantId);
        stored.PublishedJson.Should().Contain("#8a2be2");
        stored.PreviousJson.Should().Contain("#16a34a");
        stored.Revision.Should().Be(4);
    }

    [Fact]
    public async Task A_write_that_read_the_same_revision_as_a_saved_one_is_a_conflict()
    {
        // Arrange: two requests load revision 1, one saves first
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        await PublishAsync(tenantId, GREEN, 0);
        await using var slowDb = CreateDb(tenantId);
        _ = await slowDb.Set<TenantBranding>().SingleAsync(Cancellation);
        await PublishAsync(tenantId, PURPLE, 1);

        // Act
        var result = await PublishAsync(slowDb, tenantId, new PublishTenantBrandingCommand(BLUE.Replace("{tenant}", tenantId.ToString(), StringComparison.Ordinal), 1));

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        slowDb.ChangeTracker.Entries<TenantBranding>().Should().BeEmpty("a refused write must not be replayed by a later save");
        var retry = await PublishAsync(slowDb, tenantId, new PublishTenantBrandingCommand(BLUE.Replace("{tenant}", tenantId.ToString(), StringComparison.Ordinal), 2));
        retry.IsSuccess.Should().BeTrue();
        var stored = await ReadAsync(tenantId);
        stored.PublishedJson.Should().Contain("#0052d1");
        stored.PreviousJson.Should().Contain("#8a2be2");
        stored.Revision.Should().Be(3);
    }

    [Fact]
    public async Task A_second_first_insert_is_a_conflict_and_leaves_nothing_tracked()
    {
        // Arrange: a request found no row and starts one; another request inserts the tenant's row first
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        await using var slowDb = CreateDb(tenantId);
        var slow = new TenantBranding(tenantId);
        slow.Publish(BrandOverride.Parse(PURPLE, tenantId).Value, 0, DateTime.UtcNow);
        slowDb.Add(slow);
        await PublishAsync(tenantId, GREEN, 0);

        // Act
        var save = () => slowDb.SaveChangesAsync(Cancellation);

        // Assert
        await save.Should().ThrowAsync<TenantBrandingConflictException>();
        slowDb.ChangeTracker.Entries<TenantBranding>().Should().BeEmpty("the losing insert must not be replayed by a later save");
        (await ReadAsync(tenantId)).PublishedJson.Should().Contain("#16a34a");
    }

    private async Task<Result<TenantBrandingDto>> PublishAsync(long tenantId, string json, long revision)
    {
        await using var db = CreateDb(tenantId);
        return await PublishAsync(db, tenantId, new PublishTenantBrandingCommand(json, revision));
    }

    private static Task<Result<TenantBrandingDto>> PublishAsync(AppDbContext db, long tenantId, PublishTenantBrandingCommand command) =>
        new PublishTenantBrandingHandler(Repository(db), new IntegrationTenantContext(tenantId), new AllowAllTenantEntitlements(), new DateTimeProvider())
            .Handle(command, Cancellation);

    private async Task<Result<TenantBrandingDto>> RollbackAsync(long tenantId, long revision)
    {
        await using var db = CreateDb(tenantId);
        return await new RollbackTenantBrandingHandler(Repository(db), new IntegrationTenantContext(tenantId), new AllowAllTenantEntitlements(), new DateTimeProvider())
            .Handle(new RollbackTenantBrandingCommand(revision), Cancellation);
    }

    private async Task<TenantBranding> ReadAsync(long tenantId)
    {
        await using var db = CreateDb(tenantId);
        return await db.Set<TenantBranding>().AsNoTracking().SingleAsync(Cancellation);
    }

    private static EfRepository<TenantBranding> Repository(AppDbContext db) => new(db, new EndatixSpecificationEvaluator([]));

    private AppDbContext CreateDb(long tenantId)
    {
        DbContextOptionsBuilder<AppDbContext> options = new();
        IntegrationAppDbContextFactory.ConfigureOptions(options, fixture.Database.ConnectionString, fixture.Database.Provider);
        return new AppDbContext(options.Options, new IntegrationTenantContext(tenantId), new OutboxIntegrationEventDispatcher());
    }
}
