using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Idempotent enqueue against PostgreSQL, where the unique index that guarantees it is enforced. Every node here
/// only schedules, so the rows and triggers counted are exactly what enqueueing wrote.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class BackgroundJobDedupTests(DbIntegrationFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    private const string JobType = "T";

    [Fact]
    public async Task EnqueueAsync_SameDedupKey_ReturnsExistingJob()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartScheduleOnlyNodeAsync(database, ct);

        // Act
        var first = await node.EnqueueAsync(Request(tenantId: 5, "42:T"), ct);
        var second = await node.EnqueueAsync(Request(tenantId: 5, "42:T"), ct);

        // Assert
        second.Should().Be(first);
        (await CountRowsAsync(database, ct)).Should().Be(1);
        (await CountTriggersAsync(database, ct)).Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_NoDedupKey_CreatesIndependentJobs()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartScheduleOnlyNodeAsync(database, ct);

        // Act
        var first = await node.EnqueueAsync(Request(tenantId: 5, dedupKey: null), ct);
        var second = await node.EnqueueAsync(Request(tenantId: 5, dedupKey: null), ct);

        // Assert
        second.Should().NotBe(first);
        (await CountRowsAsync(database, ct)).Should().Be(2);
    }

    [Fact]
    public async Task EnqueueManyAsync_PartialCollision_ReturnsExistingAndNewIdsInOrder()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartScheduleOnlyNodeAsync(database, ct);
        var existingId = await node.EnqueueAsync(Request(tenantId: 5, "k1"), ct);
        var rowsBefore = await CountRowsAsync(database, ct);
        var triggersBefore = await CountTriggersAsync(database, ct);

        // Act
        var ids = await node.EnqueueManyAsync([Request(tenantId: 5, "k1"), Request(tenantId: 5, "k2")], ct);

        // Assert
        ids.Should().HaveCount(2);
        ids[0].Should().Be(existingId);
        ids[1].Should().NotBe(existingId);
        (await CountRowsAsync(database, ct)).Should().Be(rowsBefore + 1);
        (await CountTriggersAsync(database, ct)).Should().Be(triggersBefore + 1);
    }

    [Fact]
    public async Task EnqueueAsync_SameKeyOtherTenant_CreatesSecondJob()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartScheduleOnlyNodeAsync(database, ct);

        // Act
        var forTenant5 = await node.EnqueueAsync(Request(tenantId: 5, "42:T"), ct);
        var forTenant6 = await node.EnqueueAsync(Request(tenantId: 6, "42:T"), ct);

        // Assert
        forTenant6.Should().NotBe(forTenant5);
        (await CountRowsAsync(database, ct)).Should().Be(2);
    }

    [Fact]
    public async Task EnqueueAsync_ConcurrentSameKey_CreatesOneJob()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartScheduleOnlyNodeAsync(database, ct);

        // Act — each call on its own scope, so each has its own context and transaction.
        var ids = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var scope = node.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>()
                .EnqueueAsync(Request(tenantId: 5, "concurrent:T"), ct);
        }));

        // Assert
        (await CountRowsAsync(database, ct)).Should().Be(1);
        ids.Distinct().Should().ContainSingle();
        (await CountTriggersAsync(database, ct)).Should().Be(1);
    }

    private static BackgroundJobRequest Request(long tenantId, string? dedupKey) =>
        new(JobType, """{"same":"payload"}""", tenantId, DedupKey: dedupKey);

    private static async Task<JobsTestNode> StartScheduleOnlyNodeAsync(JobsTestDatabase database, CancellationToken ct)
    {
        var node = JobsTestNode.Create(
            database.ConnectionString,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await node.StartAsync(ct);
        return node;
    }

    private static Task<long> CountRowsAsync(JobsTestDatabase database, CancellationToken ct) =>
        database.CountAsync("""SELECT count(*) FROM jobs."BackgroundJobs" """, ct);

    private static Task<long> CountTriggersAsync(JobsTestDatabase database, CancellationToken ct) =>
        database.CountAsync("SELECT count(*) FROM jobs.qrtz_triggers", ct);
}
