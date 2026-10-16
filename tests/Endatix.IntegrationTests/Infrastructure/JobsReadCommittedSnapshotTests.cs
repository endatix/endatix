using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Quartz;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Modules.Jobs.Runtime;

namespace Endatix.IntegrationTests;

/// <summary>
/// A SQL Server node that enqueues without the trigger lock needs the database to read committed snapshots; one that
/// takes the lock, as SQL Server nodes do by default, needs nothing. Each test runs on a database of its own, whose
/// snapshot setting it chooses.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "SqlServer")]
public sealed class JobsReadCommittedSnapshotTests(DbIntegrationFixture fixture)
{
    private const string SkipReason = "Asserts SQL Server's READ_COMMITTED_SNAPSHOT database option.";

    private static readonly Dictionary<string, string?> LockFree =
        new() { ["Endatix:BackgroundJobs:LockOnInsert"] = "false" };

    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Node_refuses_lock_free_enqueue_without_read_committed_snapshot()
    {
        // Arrange — a job waits in the store, enqueued by a node that takes the lock, as the default does.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var waitingJobId = await EnqueueFromALockingNodeAsync(database, ct);
        var invocations = new ProbeInvocations();
        await using var node = JobsTestNode.Create(database, LockFree, services => services.AddProbe(invocations));

        // Act
        var start = () => node.StartAsync(ct);

        // Assert — refused before its scheduler was built, so it never acquires the waiting job.
        (await start.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("Endatix:BackgroundJobs:LockOnInsert").And.Contain("READ_COMMITTED_SNAPSHOT");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        invocations.CountFor(waitingJobId).Should().Be(0);
        (await TriggerStateAsync(database, waitingJobId, ct)).Should().Be("WAITING");
    }

    [Fact]
    public async Task Refused_lock_free_node_stores_no_job_it_is_asked_to_enqueue()
    {
        // Arrange — a node refused at its start stays up when the refusal comes after a deferred start, and its
        // queue must not store triggers lock-free in the meantime.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await using var node = JobsTestNode.Create(database, LockFree);
        var start = () => node.StartAsync(ct);
        await start.Should().ThrowAsync<InvalidOperationException>();

        // Act
        var enqueue = () => node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), ct);

        // Assert
        (await enqueue.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("READ_COMMITTED_SNAPSHOT");
        (await database.CountAsync("""SELECT count(*) FROM jobs."BackgroundJobs" """, ct)).Should().Be(0);
        (await database.CountAsync(
            "SELECT count(*) FROM jobs.qrtz_TRIGGERS WHERE TRIGGER_GROUP <> 'endatix-maintenance'", ct)).Should().Be(0);
    }

    [Fact]
    public async Task Refused_node_hands_no_scheduler_to_the_dashboard_or_any_other_caller()
    {
        // Arrange — the dashboard and its HTTP API write through the scheduler the module's factory builds.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await using var node = JobsTestNode.Create(database, LockFree);
        var start = () => node.StartAsync(ct);
        await start.Should().ThrowAsync<InvalidOperationException>();
        var schedulers = node.Services.GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName);

        // Act — a write as "Trigger now" makes one: get the scheduler, store a trigger for the probe's job.
        var write = async () =>
        {
            var scheduler = await schedulers.GetScheduler(ct);
            await scheduler.ScheduleJob(
                TriggerBuilder.Create().ForJob(QuartzRegistration.JobKeyFor(ProbePayload.JobType)).StartNow().Build(),
                cancellationToken: ct);
        };

        // Assert
        (await write.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("READ_COMMITTED_SNAPSHOT");
        var lookup = () => schedulers.LookupScheduler(QuartzRegistration.SchedulerName, ct).AsTask();
        await lookup.Should().ThrowAsync<InvalidOperationException>("looking a scheduler up by name builds it");
        (await database.CountAsync("SELECT count(*) FROM jobs.qrtz_TRIGGERS WHERE TRIGGER_GROUP <> 'endatix-maintenance'", ct))
            .Should().Be(0);
    }

    [Fact]
    public async Task Refused_node_keeps_refusing_until_it_restarts()
    {
        // Arrange — the node keeps its answer, so turning snapshots on reaches it only through a restart.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await using var refused = JobsTestNode.Create(database, LockFree);
        var start = () => refused.StartAsync(ct);
        await start.Should().ThrowAsync<InvalidOperationException>();
        await database.ExecuteAsync(
            "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE", ct);

        // Act
        var enqueueOnRefused = () => refused.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), ct);
        var ranAfterRestart = await StartAndRunAJobAsync(database, LockFree, ct);

        // Assert
        (await enqueueOnRefused.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("READ_COMMITTED_SNAPSHOT");
        ranAfterRestart.Should().BeTrue("a node started after the DBA's change checks the database afresh");
    }

    [Fact]
    public async Task Node_starts_lock_free_with_read_committed_snapshot()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await database.ExecuteAsync(
            "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE", ct);

        // Act
        var ran = await StartAndRunAJobAsync(database, LockFree, ct);

        // Assert
        ran.Should().BeTrue("a node that may enqueue lock-free runs jobs once the database reads committed snapshots");
    }

    [Fact]
    public async Task Node_starts_with_default_lock_on_insert_without_read_committed_snapshot()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);

        // Act
        var ran = await StartAndRunAJobAsync(database, settings: null, ct);

        // Assert
        ran.Should().BeTrue("a node that takes the trigger lock on insert needs no snapshot reads");
    }

    // Whether a node started with these settings runs a job it enqueues.
    private static async Task<bool> StartAndRunAJobAsync(
        JobsTestDatabase database,
        Dictionary<string, string?>? settings,
        CancellationToken ct)
    {
        var invocations = new ProbeInvocations();
        await using var node = JobsTestNode.Create(database, settings, services => services.AddProbe(invocations));
        await node.StartAsync(ct);
        var jobId = await node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), ct);
        return await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 1), RunTimeout, ct);
    }

    // A schedule-only node, stopped once it has enqueued, so the job's trigger is left waiting.
    private static async Task<long> EnqueueFromALockingNodeAsync(JobsTestDatabase database, CancellationToken ct)
    {
        await using var node = JobsTestNode.Create(
            database,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await node.StartAsync(ct);
        var jobId = await node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), ct);
        await node.StopAsync(ct);
        return jobId;
    }

    private static async Task<string> TriggerStateAsync(JobsTestDatabase database, long jobId, CancellationToken ct)
    {
        var states = await database.QueryAsync(
            $"SELECT TRIGGER_STATE FROM jobs.qrtz_TRIGGERS WHERE TRIGGER_NAME = '{jobId}'",
            reader => reader.GetString(0),
            ct);
        return states.Single();
    }
}
