using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

/// <summary>
/// Each job type is its own execution group on each node: a busy type never takes another's slot, and a node
/// never takes a job type it has no handler for.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class JobTypeIsolationTests(DbIntegrationFixture fixture)
{
    private const string Export = "SubmissionExport";
    private const string WebHook = "WebHookDelivery";

    // Twice a node's idle wait of 1 s: long enough for its scheduler to acquire another due trigger, had the cap let it.
    private static readonly TimeSpan NodeAcquisitionWindow = TimeSpan.FromSeconds(2);

    // Past the 5 s misfire threshold the test sets, and the misfire scan's interval, which is that threshold too.
    private static readonly TimeSpan MisfireJump = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Webhook_jobs_keep_their_slots_while_an_export_runs()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var runs = new NamedProbeRuns();
        await using var node = JobsTestNode.Create(
            database,
            new Dictionary<string, string?>
            {
                [$"Endatix:BackgroundJobs:JobTypes:{Export}:MaxConcurrency"] = "1",
                [$"Endatix:BackgroundJobs:JobTypes:{WebHook}:MaxConcurrency"] = "4",
                ["Endatix:BackgroundJobs:ShutdownWaitSeconds"] = "1",
            },
            services => AddNamedProbes(services, runs, Export, WebHook));
        using var releaseAtTeardown = runs.ReleaseAllOnDispose();
        await node.StartAsync(ct);
        var firstExport = await node.EnqueueAsync(NamedProbeHandler.HeldUntilReleased(Export), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(runs.HasStarted(firstExport)), TimeSpan.FromSeconds(15), ct);

        // Act — the second export is older than every webhook job, so it heads the queue. The first export and the
        // webhooks hold their slots until released, so a fifth webhook could only start past its cap, and the second
        // export, which holds nothing, would have completed.
        var secondExport = await node.EnqueueAsync(NamedProbeHandler.Request(Export), ct);
        var webhooks = await node.EnqueueManyAsync(
            Enumerable.Range(0, 8).Select(_ => NamedProbeHandler.HeldUntilReleased(WebHook)).ToList(),
            ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(runs.Running(WebHook) >= 4), TimeSpan.FromSeconds(15), ct);
        await Task.Delay(NodeAcquisitionWindow, ct);
        var processingWebhooks = await database.CountAsync(
            $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "Status" = 1 AND "Id" IN ({webhooks.IdList()})""", ct);
        var peakWebhooks = runs.PeakConcurrency(WebHook);
        var peakExports = runs.PeakConcurrency(Export);
        var secondExportWhileFirstRuns = await database.ReadJobAsync(secondExport, ct);
        runs.Release(Export);
        runs.Release(WebHook);
        var firstEnded = await database.WaitForStatusAsync(
            new ExpectedJobStatus(firstExport, JobStatus.Completed, TimeSpan.FromSeconds(15)), ct);
        var secondExportAfterwards = await database.WaitForStatusAsync(
            new ExpectedJobStatus(secondExport, JobStatus.Completed, TimeSpan.FromSeconds(15)), ct);

        // Assert
        processingWebhooks.Should().Be(4);
        peakWebhooks.Should().Be(4);
        peakExports.Should().Be(1);
        secondExportWhileFirstRuns!.Status.Should().Be(JobStatus.Pending);
        firstEnded.Status.Should().Be(JobStatus.Completed);
        secondExportAfterwards.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task Batched_acquisition_never_runs_more_of_a_job_type_than_its_cap()
    {
        // Arrange — a backlog of both job types, the exports oldest, waits before the worker starts, so a batch the
        // size of the worker's pool could be filled with exports alone.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await using var scheduleOnly = JobsTestNode.Create(
            database, new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await scheduleOnly.StartAsync(ct);
        var exports = await scheduleOnly.EnqueueManyAsync(Requests(Export, count: 12, holdMilliseconds: 500), ct);
        var webhooks = await scheduleOnly.EnqueueManyAsync(Requests(WebHook, count: 12, holdMilliseconds: 500), ct);
        var runs = new NamedProbeRuns();
        using var batches = TriggerBatchRecorder.Start("batching-worker");
        await using var worker = JobsTestNode.Create(
            database,
            new Dictionary<string, string?>
            {
                ["Endatix:BackgroundJobs:Clustering:InstanceId"] = "batching-worker",
                [$"Endatix:BackgroundJobs:JobTypes:{Export}:MaxConcurrency"] = "2",
                [$"Endatix:BackgroundJobs:JobTypes:{WebHook}:MaxConcurrency"] = "3",
            },
            services => AddNamedProbes(services, runs, Export, WebHook));

        // Act
        await worker.StartAsync(ct);
        var allCompleted = await AllCompletedOnFirstAttemptAsync(database, [.. exports, .. webhooks], ct);

        // Assert — acquisitions took several triggers at once, and each type still ran exactly up to its cap.
        allCompleted.Should().BeTrue();
        batches.Largest.Should().BeGreaterThan(1);
        runs.PeakConcurrency(Export).Should().Be(2);
        runs.PeakConcurrency(WebHook).Should().Be(3);
    }

    [Fact]
    public async Task Recovered_jobs_of_a_crashed_node_run_within_the_survivors_cap()
    {
        // Arrange — the crashed node runs three webhooks at once; the survivor's cap is one.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var runsOnA = new NamedProbeRuns();
        var runsOnB = new NamedProbeRuns();
        await using var nodeA = JobsTestNode.Create(
            database,
            new Dictionary<string, string?> { [$"Endatix:BackgroundJobs:JobTypes:{WebHook}:MaxConcurrency"] = "3" },
            services => AddNamedProbes(services, runsOnA, WebHook));
        await nodeA.StartAsync(ct);
        var jobIds = await nodeA.EnqueueManyAsync(
            Enumerable.Range(0, 3).Select(_ => NamedProbeHandler.BlockingFirstAttempt(WebHook)).ToList(),
            ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(runsOnA.Count == 3), TimeSpan.FromSeconds(15), ct);
        await using var nodeB = JobsTestNode.Create(
            database,
            new Dictionary<string, string?>
            {
                [$"Endatix:BackgroundJobs:JobTypes:{WebHook}:MaxConcurrency"] = "1",
                ["Endatix:BackgroundJobs:ShutdownWaitSeconds"] = "1",
            },
            services => AddNamedProbes(services, runsOnB, WebHook));
        using var releaseAtTeardown = runsOnB.ReleaseAllOnDispose();
        await nodeB.StartAsync(ct);

        // Act — B recovers all three of A's jobs at once, and holds the first it runs, so a second could only start
        // by taking a slot past B's cap.
        await nodeA.KillAsync();
        await JobsTestWait.UntilAsync(() => Task.FromResult(runsOnB.Count >= 1), TimeSpan.FromSeconds(30), ct);
        await Task.Delay(NodeAcquisitionWindow, ct);
        var startedWhileHeld = runsOnB.Count;
        runsOnB.Release(WebHook);
        var allRecovered = await AllCompletedOnRecoveryAsync(database, jobIds, ct);

        // Assert
        startedWhileHeld.Should().Be(1);
        allRecovered.Should().BeTrue();
        runsOnB.Count.Should().Be(3);
        runsOnB.PeakConcurrency(WebHook).Should().Be(1);
    }

    [Fact]
    public async Task Host_without_handler_never_claims_its_jobs()
    {
        // Arrange — host A runs TypeR; host B runs something else, on the same store.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var runsOnA = new NamedProbeRuns();
        var runsOnB = new NamedProbeRuns();
        var logsOnB = new CapturingLoggerProvider();
        await using var hostB = JobsTestNode.Create(
            database,
            configureServices: services =>
            {
                AddNamedProbes(services, runsOnB, "OtherType");
                services.AddSingleton<ILoggerProvider>(logsOnB);
            });
        await hostB.StartAsync(ct);

        // Act — enqueued while only B is running, so B has every chance to take them.
        var jobIds = await hostB.EnqueueManyAsync(
            Enumerable.Range(0, 5).Select(_ => NamedProbeHandler.Request("TypeR")).ToList(), ct);
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        var claimedWhileOnlyB = await database.CountAsync(
            $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "AttemptCount" > 0 AND "Id" IN ({jobIds.IdList()})""", ct);
        await using var hostA = JobsTestNode.Create(
            database,
            configureServices: services => AddNamedProbes(services, runsOnA, "TypeR"));
        await hostA.StartAsync(ct);
        var allCompleted = await AllCompletedOnFirstAttemptAsync(database, jobIds, ct);

        // Assert — B never fired them (not even to decline), and A ran each exactly once.
        claimedWhileOnlyB.Should().Be(0);
        runsOnB.Count.Should().Be(0);
        logsOnB.Entries.Should().NotContain(entry => entry.Message.Contains("without its handler"));
        allCompleted.Should().BeTrue();
        runsOnA.Count.Should().Be(5);
    }

    [Fact]
    public async Task Misfired_trigger_logs_backlog_warning()
    {
        // Arrange — one webhook slot, on a clock the test moves.
        var ct = TestContext.Current.CancellationToken;
        using var misfires = JobMisfireCounter.Start();
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var clock = new JobsTestClock();
        var runs = new NamedProbeRuns();
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            database,
            new Dictionary<string, string?>(JobsTestClock.SlowSchedulerTimings())
            {
                [$"Endatix:BackgroundJobs:JobTypes:{WebHook}:MaxConcurrency"] = "1",
                ["Endatix:BackgroundJobs:MisfireThresholdSeconds"] = "5",
                ["Endatix:BackgroundJobs:ShutdownWaitSeconds"] = "1",
            },
            services =>
            {
                AddNamedProbes(services, runs, WebHook);
                services.AddSingleton<ILoggerProvider>(logs);
                clock.Use(services);
            });
        using var releaseAtTeardown = runs.ReleaseAllOnDispose();
        await node.StartAsync(ct);
        var first = await node.EnqueueAsync(NamedProbeHandler.HeldUntilReleased(WebHook), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(runs.HasStarted(first)), TimeSpan.FromSeconds(15), ct);
        var secondId = (await node.EnqueueAsync(NamedProbeHandler.Request(WebHook), ct)).ToString();

        // Act — the second job waits behind the first, which keeps the only slot, while the clock jumps once past the
        // misfire threshold and the misfire scan's interval.
        await clock.AdvanceAsync(MisfireJump, ct);
        var warned = await JobsTestWait.UntilAsync(
            () => Task.FromResult(logs.Entries.Any(entry =>
                entry.Message.Contains("waited past the misfire threshold") && entry.Message.Contains(secondId))),
            TimeSpan.FromSeconds(15),
            ct);
        runs.Release(WebHook);

        // Assert
        warned.Should().BeTrue();
        misfires.JobTypes.Should().Contain(WebHook);
    }

    private static List<BackgroundJobRequest> Requests(string jobType, int count, int holdMilliseconds) =>
        Enumerable.Range(0, count).Select(_ => NamedProbeHandler.Request(jobType, holdMilliseconds)).ToList();

    // Each recovered job completed on its second attempt: the crashed node's first one never recorded an outcome.
    private static Task<bool> AllCompletedOnRecoveryAsync(
        JobsTestDatabase database,
        IReadOnlyList<long> jobIds,
        CancellationToken ct) =>
        JobsTestWait.UntilAsync(
            async () => await database.CountAsync(
                $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "Status" = 3 AND "AttemptCount" = 2 AND "Id" IN ({jobIds.IdList()})""",
                ct) == jobIds.Count,
            TimeSpan.FromSeconds(60),
            ct);

    // Every job ran once, and none of them twice, whichever node ran it.
    private static Task<bool> AllCompletedOnFirstAttemptAsync(
        JobsTestDatabase database,
        IReadOnlyList<long> jobIds,
        CancellationToken ct) =>
        JobsTestWait.UntilAsync(
            async () => await database.CountAsync(
                $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "Status" = 3 AND "AttemptCount" = 1 AND "Id" IN ({jobIds.IdList()})""",
                ct) == jobIds.Count,
            TimeSpan.FromSeconds(30),
            ct);

    private static void AddNamedProbes(IServiceCollection services, NamedProbeRuns runs, params string[] jobTypes)
    {
        foreach (var jobType in jobTypes)
        {
            services.AddBackgroundJobHandler(jobType, _ => new NamedProbeHandler(jobType, runs));
        }
    }
}
