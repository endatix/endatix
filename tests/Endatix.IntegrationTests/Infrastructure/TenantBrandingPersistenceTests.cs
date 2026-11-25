using Endatix.Core.Branding;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Publishing;
using Endatix.Core.UseCases.TenantBranding.Restoring;
using Endatix.Core.Versioning;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Repositories;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

/// <summary>
/// A tenant's branding against the real database: the JSON columns (jsonb on PostgreSQL) keep publish idempotent after
/// a reload, history is only appended, and the version token and the keys turn a lost race into a conflict.
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
    public async Task Publishing_the_live_value_again_after_a_reload_changes_nothing()
    {
        // Arrange
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        await PublishAsync(tenantId, Blue(tenantId), 0);
        await PublishAsync(tenantId, GREEN, 1);

        // Act: a retry of the second publish, with the version read before it
        var retry = await PublishAsync(tenantId, GREEN, 1);

        // Assert
        retry.IsSuccess.Should().BeTrue();
        var stored = await ReadAsync(tenantId);
        stored.Version.Should().Be(2);
        stored.PublishedNumber.Should().Be(2);
        stored.PublishedJson.Should().Contain("#16a34a");
        var history = await ReadHistoryAsync(tenantId);
        history.Select(version => version.Number).Should().Equal(1, 2);
        history[0].Json.Should().Contain("#0052d1").And.Contain($"t/{tenantId}/brand/icon.png");
        history[0].ContentHash.Should().Be(BrandOverride.Parse(Blue(tenantId), tenantId).Value.ContentHash);
        history[0].Source.Should().Be(ContentVersionSources.PUBLISH);
    }

    [Fact]
    public async Task Rolling_back_twice_toggles_and_keeps_every_version()
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
        stored.PublishedNumber.Should().Be(4);
        stored.Version.Should().Be(4);
        (await ReadHistoryAsync(tenantId))
            .Select(version => $"{version.Number}:{version.Source}<{version.RestoredFromNumber}")
            .Should().Equal("1:publish<", "2:publish<", "3:restore<1", "4:restore<2");
    }

    [Fact]
    public async Task A_write_that_read_the_same_version_as_a_saved_one_is_a_conflict()
    {
        // Arrange: two requests load version 1, one saves first
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        await PublishAsync(tenantId, GREEN, 0);
        await using var slowDb = CreateDb(tenantId);
        _ = await Repository(slowDb).FindAsync(tenantId, Cancellation);
        await PublishAsync(tenantId, PURPLE, 1);

        // Act
        var result = await PublishAsync(slowDb, tenantId, new PublishTenantBrandingCommand(Blue(tenantId), 1));

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        slowDb.ChangeTracker.Entries().Should().BeEmpty("a refused write must not be replayed by a later save");
        var retry = await PublishAsync(slowDb, tenantId, new PublishTenantBrandingCommand(Blue(tenantId), 2));
        retry.IsSuccess.Should().BeTrue();
        var stored = await ReadAsync(tenantId);
        stored.PublishedJson.Should().Contain("#0052d1");
        stored.Version.Should().Be(3);
        (await ReadHistoryAsync(tenantId)).Select(version => version.Json)
            .Should().SatisfyRespectively(
                green => green.Should().Contain("#16a34a"),
                purple => purple.Should().Contain("#8a2be2"),
                blue => blue.Should().Contain("#0052d1"));
    }

    [Fact]
    public async Task A_second_first_publish_is_conceded_and_leaves_nothing_tracked()
    {
        // Arrange: a request found no row and starts one; another request publishes the tenant's first version first
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        await using var slowDb = CreateDb(tenantId);
        var slow = new TenantBranding(tenantId);
        var slowVersion = slow.Publish(BrandOverride.Parse(PURPLE, tenantId).Value, 0, new ContentVersionStamp(DateTime.UtcNow, null)).Value!;
        await PublishAsync(tenantId, GREEN, 0);

        // Act
        var saved = await Repository(slowDb).SaveOrConcedeAsync(slow, slowVersion, Cancellation);

        // Assert
        saved.Should().BeFalse();
        slowDb.ChangeTracker.Entries().Should().BeEmpty("the losing insert must not be replayed by a later save");
        (await ReadAsync(tenantId)).PublishedJson.Should().Contain("#16a34a");
        (await ReadHistoryAsync(tenantId)).Should().ContainSingle().Which.Json.Should().Contain("#16a34a");
    }

    private static string Blue(long tenantId) => BLUE.Replace("{tenant}", tenantId.ToString(), StringComparison.Ordinal);

    private async Task<Result<TenantBrandingDto>> PublishAsync(long tenantId, string json, long version)
    {
        await using var db = CreateDb(tenantId);
        return await PublishAsync(db, tenantId, new PublishTenantBrandingCommand(json, version));
    }

    private static Task<Result<TenantBrandingDto>> PublishAsync(AppDbContext db, long tenantId, PublishTenantBrandingCommand command) =>
        new PublishTenantBrandingHandler(Repository(db), Access(tenantId), new DateTimeProvider())
            .Handle(command, Cancellation);

    private async Task<Result<TenantBrandingDto>> RollbackAsync(long tenantId, long version)
    {
        await using var db = CreateDb(tenantId);
        return await new RestoreTenantBrandingHandler(Repository(db), Access(tenantId), new DateTimeProvider())
            .Handle(new RollbackTenantBrandingCommand(version), Cancellation);
    }

    private static TenantBrandingAccess Access(long tenantId) =>
        new(new IntegrationTenantContext(tenantId), new AllowAllTenantEntitlements(), new());

    private async Task<TenantBranding> ReadAsync(long tenantId)
    {
        await using var db = CreateDb(tenantId);
        return await db.Set<TenantBranding>().AsNoTracking().SingleAsync(Cancellation);
    }

    private async Task<List<TenantBrandingVersion>> ReadHistoryAsync(long tenantId)
    {
        await using var db = CreateDb(tenantId);
        return await db.Set<TenantBrandingVersion>().AsNoTracking().OrderBy(version => version.Number).ToListAsync(Cancellation);
    }

    private static TenantBrandingRepository Repository(AppDbContext db) => new(db, new UniqueConstraintViolationChecker());

    private AppDbContext CreateDb(long tenantId)
    {
        DbContextOptionsBuilder<AppDbContext> options = new();
        IntegrationAppDbContextFactory.ConfigureOptions(options, fixture.Database.ConnectionString, fixture.Database.Provider);
        return new AppDbContext(options.Options, new IntegrationTenantContext(tenantId), new OutboxIntegrationEventDispatcher());
    }
}
