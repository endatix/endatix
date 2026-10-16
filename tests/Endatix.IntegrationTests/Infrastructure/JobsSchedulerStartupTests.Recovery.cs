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
        // Arrange — a migrated database taken away, as in a failover: the node defers its start, and an enqueue made
        // meanwhile asks Quartz to build the scheduler, which fails and shuts that build's parts down.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await using (var migrating = JobsTestNode.Create(
            database, new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" }))
        {
            await migrating.StartAsync(ct);
            await migrating.StopAsync(ct);
        }

        await database.TakeAwayAsync(ct);
        var invocations = new ProbeInvocations();
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            database,
            configureServices: services =>
            {
                services.AddProbe(invocations);
                services.AddSingleton<ILoggerProvider>(logs);
            },
            migrate: false);
        await node.StartAsync(ct);
        var enqueueWhileAway = () => node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), ct);
        await enqueueWhileAway.Should().ThrowAsync<Exception>();
        await enqueueWhileAway.Should().ThrowAsync<Exception>("a rebuild while the database is still away fails too");

        // Act
        await database.BringBackAsync(ct);
        var jobId = await EnqueueOnceTheSchedulerIsBackAsync(node, ct);
        var ran = await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 1), Recovery, ct);

        // Assert — the node rebuilt its scheduler, started it, and runs jobs, with nothing left for a restart to fix.
        ran.Should().BeTrue();
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
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
