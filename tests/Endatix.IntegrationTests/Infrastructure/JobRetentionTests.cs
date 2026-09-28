using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The retention job against PostgreSQL: which rows it deletes, how far one run goes, and the expiry every terminal
/// write stamps.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class JobRetentionTests(DbIntegrationFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    [Fact]
    public async Task RunAsync_ExpiredTerminalRows_DeletesOnlyThose()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartScheduleOnlyNodeAsync(database, ct);
        var now = DateTime.UtcNow;
        List<long> expired =
        [
            await SeedAsync(node, JobStatus.Completed, now.AddMinutes(-1), ct),
            await SeedAsync(node, JobStatus.Completed, now.AddMinutes(-1), ct),
            await SeedAsync(node, JobStatus.Completed, now.AddMinutes(-1), ct),
        ];
        List<long> kept =
        [
            await SeedAsync(node, JobStatus.Completed, now.AddHours(1), ct),
            await SeedAsync(node, JobStatus.Pending, now.AddMinutes(-1), ct),
            await SeedAsync(node, JobStatus.Retrying, now.AddMinutes(-1), ct),
        ];

        // Act
        var deleted = await RunRetentionAsync(node, ct);

        // Assert
        deleted.Should().Be(3);
        var remaining = await database.QueryAsync("""SELECT "Id" FROM jobs."BackgroundJobs" """, reader => reader.GetInt64(0), ct);
        remaining.Should().BeEquivalentTo(kept);
        remaining.Should().NotContain(expired);
    }

    [Fact]
    public async Task RunAsync_BatchLimits_StopsAfterMaxBatches()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartScheduleOnlyNodeAsync(database, ct, new Dictionary<string, string?>
        {
            ["Endatix:BackgroundJobs:Retention:BatchSize"] = "2",
            ["Endatix:BackgroundJobs:Retention:MaxBatchesPerRun"] = "1",
        });
        for (var i = 0; i < 5; i++)
        {
            await SeedAsync(node, JobStatus.Completed, DateTime.UtcNow.AddMinutes(-1), ct);
        }

        // Act
        var deleted = await RunRetentionAsync(node, ct);

        // Assert
        deleted.Should().Be(2);
        (await database.CountAsync("""SELECT count(*) FROM jobs."BackgroundJobs" """, ct)).Should().Be(3);
    }

    [Fact]
    public async Task TerminalWrite_NullExpiresAt_SetsRetention()
    {
        // Arrange — a webhook delivery job, whose rows are kept three days.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = JobsTestNode.Create(
            database.ConnectionString,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:JobTypes:WebHookDelivery:RetentionDays"] = "3" },
            services => services.AddScoped<IBackgroundJobHandler>(_ => new NamedProbeHandler("WebHookDelivery", new NamedProbeRuns())));
        await node.StartAsync(ct);

        // Act
        var jobId = await node.EnqueueAsync(NamedProbeHandler.Request("WebHookDelivery"), ct);
        var row = await database.WaitForStatusAsync(jobId, status => status == JobStatus.Completed, TimeSpan.FromSeconds(30), ct);

        // Assert
        row.CompletedAt.Should().NotBeNull();
        row.ExpiresAt.Should().Be(row.CompletedAt!.Value.AddDays(3));
    }

    private static async Task<JobsTestNode> StartScheduleOnlyNodeAsync(
        JobsTestDatabase database,
        CancellationToken ct,
        Dictionary<string, string?>? settings = null)
    {
        var all = new Dictionary<string, string?>(settings ?? []) { ["Endatix:BackgroundJobs:RunInProcess"] = "false" };
        var node = JobsTestNode.Create(database.ConnectionString, all);
        await node.StartAsync(ct);
        return node;
    }

    private static async Task<int> RunRetentionAsync(JobsTestNode node, CancellationToken ct)
    {
        await using var scope = node.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<JobRetentionJob>().RunAsync(ct);
    }

    // Status and expiry are written directly: the entity's transitions cannot place a row in every state.
    private static async Task<long> SeedAsync(JobsTestNode node, JobStatus status, DateTime expiresAt, CancellationToken ct)
    {
        await using var scope = node.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<JobsPostgreSqlDbContext>();
        var job = new BackgroundJob("RetentionProbe", "{}", 5, DateTime.UtcNow);
        context.BackgroundJobs.Add(job);
        await context.SaveChangesAsync(ct);
        await context.BackgroundJobs
            .Where(row => row.Id == job.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Status, status)
                .SetProperty(row => row.ExpiresAt, (DateTime?)expiresAt), ct);
        return job.Id;
    }
}
