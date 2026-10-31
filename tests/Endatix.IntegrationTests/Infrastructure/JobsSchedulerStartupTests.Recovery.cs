using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

/// <summary>A start whose first scheduler build fails because the database is away, with Quartz's own factory.</summary>
public sealed partial class JobsSchedulerStartupTests
{
    private static readonly TimeSpan Recovery = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Scheduler_whose_first_build_fails_while_the_database_is_away_starts_once_it_is_back()
    {
        // Arrange — the node defers its start, and an enqueue made meanwhile asks Quartz to build the scheduler, which
        // fails and shuts that build's parts down.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await MigrateAsync(database, ct);
        var invocations = new ProbeInvocations();
        var logs = new CapturingLoggerProvider();
        await using var node = await NodeWhoseFirstBuildFailedAsync(
            database,
            services =>
            {
                services.AddProbe(invocations);
                services.AddSingleton<ILoggerProvider>(logs);
            },
            ct);

        // Act
        var jobId = await EnqueueOnceTheSchedulerIsBackAsync(node, ct);
        var ran = await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 1), Recovery, ct);

        // Assert — the node rebuilt its scheduler, started it, and runs jobs, with nothing left for a restart to fix.
        ran.Should().BeTrue();
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Scheduler_rebuilt_after_a_failed_build_is_built_as_a_normal_start_builds_it()
    {
        // Arrange — the same node built normally, and again after its first build failed and Quartz rebuilt it. The
        // retention schedule the first build stored is deleted in between, so only the rebuild can store it again.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        SchedulerShape built;
        await using (var normal = JobsTestNode.Create(database, configureServices: services => services.AddProbe(new ProbeInvocations())))
        {
            await normal.StartAsync(ct);
            built = await SchedulerShape.ReadAsync(normal, ct);
            await normal.StopAsync(ct);
        }

        await database.ExecuteAsync(DeleteRetentionSchedule, ct);

        await using var node = await NodeWhoseFirstBuildFailedAsync(database, services => services.AddProbe(new ProbeInvocations()), ct);
        await EnqueueOnceTheSchedulerIsBackAsync(node, ct);

        // Act
        var rebuilt = await SchedulerShape.ReadAsync(node, ct);

        // Assert — the same pool, store, timing, listeners, execution limits and retention schedule.
        rebuilt.Should().Be(built);
    }

    private const string DeleteRetentionSchedule = """
        DELETE FROM jobs.qrtz_CRON_TRIGGERS WHERE TRIGGER_GROUP = 'endatix-maintenance';
        DELETE FROM jobs.qrtz_TRIGGERS WHERE TRIGGER_GROUP = 'endatix-maintenance';
        DELETE FROM jobs.qrtz_JOB_DETAILS WHERE JOB_GROUP = 'endatix-maintenance';
        """;

    private static async Task MigrateAsync(JobsTestDatabase database, CancellationToken ct)
    {
        await using var migrating = JobsTestNode.Create(
            database, new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await migrating.StartAsync(ct);
        await migrating.StopAsync(ct);
    }

    // A node started with its migrated database taken away, as in a failover: two enqueues fail to build its scheduler,
    // the second while the database is still away, and then the database comes back.
    private static async Task<JobsTestNode> NodeWhoseFirstBuildFailedAsync(
        JobsTestDatabase database,
        Action<IServiceCollection> configureServices,
        CancellationToken ct)
    {
        await database.TakeAwayAsync(ct);
        var node = JobsTestNode.Create(database, configureServices: configureServices, migrate: false);
        await node.StartAsync(ct);
        var enqueueWhileAway = () => node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), ct);
        await enqueueWhileAway.Should().ThrowAsync<Exception>();
        await enqueueWhileAway.Should().ThrowAsync<Exception>("a rebuild while the database is still away fails too");
        await database.BringBackAsync(ct);
        return node;
    }

    // Enqueues fail until the scheduler is back; the first that succeeds returns its job.
    private static async Task<long> EnqueueOnceTheSchedulerIsBackAsync(JobsTestNode node, CancellationToken ct)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return await node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), ct);
            }
            catch (Exception) when (waited.Elapsed < Recovery)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
    }
}
