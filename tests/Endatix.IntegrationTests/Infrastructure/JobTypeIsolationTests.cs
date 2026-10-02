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
[Trait("DbSpecific", "PostgreSql")]
public sealed class JobTypeIsolationTests(DbIntegrationFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    private const string Export = "SubmissionExport";
    private const string WebHook = "WebHookDelivery";

    [Fact]
    public async Task Webhook_jobs_keep_their_slots_while_an_export_runs()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var runs = new NamedProbeRuns();
        await using var node = JobsTestNode.Create(
            database.ConnectionString,
            new Dictionary<string, string?>
            {
                [$"Endatix:BackgroundJobs:JobTypes:{Export}:MaxConcurrency"] = "1",
                [$"Endatix:BackgroundJobs:JobTypes:{WebHook}:MaxConcurrency"] = "4",
            },
            services => AddNamedProbes(services, runs, Export, WebHook));
        await node.StartAsync(ct);
        var firstExport = await node.EnqueueAsync(NamedProbeHandler.Request(Export, holdMilliseconds: 60_000), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(runs.HasStarted(firstExport)), TimeSpan.FromSeconds(15), ct);

        // Act — the second export is older than every webhook job, so it heads the queue.
        var secondExport = await node.EnqueueAsync(NamedProbeHandler.Request(Export, holdMilliseconds: 1_000), ct);
        var webhooks = await node.EnqueueManyAsync(
            Enumerable.Range(0, 8).Select(_ => NamedProbeHandler.Request(WebHook, holdMilliseconds: 20_000)).ToList(),
            ct);
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        var processingWebhooks = await database.CountAsync(
            $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "Status" = 1 AND "Id" IN ({webhooks.IdList()})""", ct);
        var secondExportWhileFirstRuns = await database.ReadJobAsync(secondExport, ct);
        var firstEnded = await database.WaitForStatusAsync(
            new ExpectedJobStatus(firstExport, JobStatus.Completed, TimeSpan.FromSeconds(90)), ct);
        var secondExportAfterwards = await database.WaitForStatusAsync(
            new ExpectedJobStatus(secondExport, JobStatus.Completed, TimeSpan.FromSeconds(45)), ct);

        // Assert
        processingWebhooks.Should().Be(4);
        secondExportWhileFirstRuns!.Status.Should().Be(JobStatus.Pending);
        firstEnded.Status.Should().Be(JobStatus.Completed);
        secondExportAfterwards.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task Batched_acquisition_never_runs_more_of_a_job_type_than_its_cap()
    {
        // Arrange — a backlog of both job types, the exports oldest, waits before the worker starts, so a batch the
        // size of the worker's pool could be filled with exports alone.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var scheduleOnly = JobsTestNode.Create(
            database.ConnectionString, new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await scheduleOnly.StartAsync(ct);
        var exports = await scheduleOnly.EnqueueManyAsync(Requests(Export, count: 12, holdMilliseconds: 500), ct);
        var webhooks = await scheduleOnly.EnqueueManyAsync(Requests(WebHook, count: 12, holdMilliseconds: 500), ct);
        var runs = new NamedProbeRuns();
        using var batches = TriggerBatchRecorder.Start("batching-worker");
        await using var worker = JobsTestNode.Create(
            database.ConnectionString,
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
    public async Task Host_without_handler_never_claims_its_jobs()
    {
        // Arrange — host A runs TypeR; host B runs something else, on the same store.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var runsOnA = new NamedProbeRuns();
        var runsOnB = new NamedProbeRuns();
        var logsOnB = new CapturingLoggerProvider();
        await using var hostB = JobsTestNode.Create(
            database.ConnectionString,
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
            database.ConnectionString,
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
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        using var misfires = JobMisfireCounter.Start();
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            database.ConnectionString,
            new Dictionary<string, string?>
            {
                [$"Endatix:BackgroundJobs:JobTypes:{WebHook}:MaxConcurrency"] = "1",
                ["Endatix:BackgroundJobs:MisfireThresholdSeconds"] = "5",
            },
            services =>
            {
                AddNamedProbes(services, new NamedProbeRuns(), WebHook);
                services.AddSingleton<ILoggerProvider>(logs);
            });
        await node.StartAsync(ct);

        // Act
        var jobIds = await node.EnqueueManyAsync(
            [NamedProbeHandler.Request(WebHook, holdMilliseconds: 20_000), NamedProbeHandler.Request(WebHook, holdMilliseconds: 20_000)],
            ct);
        var secondId = jobIds[1].ToString();
        var warned = await JobsTestWait.UntilAsync(
            () => Task.FromResult(logs.Entries.Any(entry =>
                entry.Message.Contains("waited past the misfire threshold") && entry.Message.Contains(secondId))),
            TimeSpan.FromSeconds(30),
            ct);

        // Assert
        warned.Should().BeTrue();
        misfires.JobTypes.Should().Contain(WebHook);
    }

    private static List<BackgroundJobRequest> Requests(string jobType, int count, int holdMilliseconds) =>
        Enumerable.Range(0, count).Select(_ => NamedProbeHandler.Request(jobType, holdMilliseconds)).ToList();

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
