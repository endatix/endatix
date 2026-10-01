using System.Diagnostics;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;

namespace Endatix.IntegrationTests;

/// <summary>
/// The job wrapper against a real scheduler store: every outcome it records, recovery, cancellation, the runtime
/// ceiling, tracing and metrics. Short retry intervals stand in for a fake clock.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class BackgroundJobExecutionIntegrationTests(DbIntegrationFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    private const long TenantId = 5;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ExecuteAsync_HandlerSucceeds_CompletesRow()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartNodeAsync(new(database, new ProbeInvocations()), ct);

        // Act
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert
        row.Status.Should().Be(JobStatus.Completed);
        row.AttemptCount.Should().Be(1);
        row.ProgressPercentage.Should().Be(100);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_HandlerReturnsFailure_FailsWithoutRetry()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartNodeAsync(new(database, new ProbeInvocations()), ct);

        // Act
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Fail), ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Failed, ct);

        // Assert
        row.Status.Should().Be(JobStatus.Failed);
        row.ErrorMessage.Should().Be(ProbeBehaviours.FailureMessage);
        row.AttemptCount.Should().Be(1);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_HandlerAlwaysThrows_DeadLettersAtMaxAttemptsWithSafeMessage()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartNodeAsync(
            new(
                database,
                new ProbeInvocations(),
                new Dictionary<string, string?>
                {
                    [ProbeKey("MaxAttempts")] = "3",
                    [ProbeKey("BackoffBaseSeconds")] = "1",
                }),
            ct);

        // Act
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Throw), ct);
        var row = await database.WaitForStatusAsync(
            new ExpectedJobStatus(jobId, JobStatus.DeadLettered, TimeSpan.FromSeconds(60)), ct);

        // Assert
        row.Status.Should().Be(JobStatus.DeadLettered);
        row.AttemptCount.Should().Be(3);
        row.ErrorMessage.Should().NotBeNull().And.NotContain("Password");
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsThenSucceeds_RetriesThenCompletes()
    {
        // Arrange — a backoff long enough to observe the row while it waits.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartNodeAsync(
            new(
                database,
                new ProbeInvocations(),
                new Dictionary<string, string?> { [ProbeKey("BackoffBaseSeconds")] = "3" }),
            ct);

        // Act
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.ThrowOnce), ct);
        var retrying = await database.WaitForStatusAsync(jobId, JobStatus.Retrying, ct);
        var completed = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert
        retrying.Status.Should().Be(JobStatus.Retrying);
        retrying.NextAttemptAt.Should().BeAfter(retrying.StartedAt!.Value);
        completed.Status.Should().Be(JobStatus.Completed);
        completed.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_UnreadablePayload_FailsOnce()
    {
        // Arrange — the column is jsonb, so unreadable input is well-formed JSON of the wrong shape.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        await using var node = await StartNodeAsync(new(database, invocations), ct);

        // Act
        var jobId = await node.EnqueueAsync(
            new BackgroundJobRequest(ProbePayload.JobType, """{"behaviour":{"not":"a string"}}""", TenantId),
            ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Failed, ct);

        // Assert
        row.Status.Should().Be(JobStatus.Failed);
        row.AttemptCount.Should().Be(1);
        invocations.CountFor(jobId).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_TerminalRow_SkipsHandler()
    {
        // Arrange — the row is finished before its trigger ever fires.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        await using var scheduleOnly = JobsTestNode.Create(
            database.ConnectionString,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await scheduleOnly.StartAsync(ct);
        var jobId = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);
        await database.ExecuteAsync(
            $"""UPDATE jobs."BackgroundJobs" SET "Status" = 3, "ModifiedAt" = '2026-01-01' WHERE "Id" = {jobId}""",
            ct);
        var before = await database.ReadJobAsync(jobId, ct);

        // Act
        await using var worker = await StartNodeAsync(new(database, invocations), ct);
        var fired = await NoTriggerLeftAsync(database, jobId, ct);

        // Assert
        fired.Should().BeTrue();
        invocations.CountFor(jobId).Should().Be(0);
        (await database.ReadJobAsync(jobId, ct)).Should().Be(before);
    }

    [Fact]
    public async Task Crashed_node_job_recovers_on_survivor_with_one_outcome()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var onA = new ProbeInvocations();
        var onB = new ProbeInvocations();
        var nodeA = await StartNodeAsync(new(database, onA, InstanceId("node-a")), ct);
        var jobId = await nodeA.EnqueueAsync(Probe(ProbeBehaviours.BlockFirst), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(onA.CountFor(jobId) == 1), Patience, ct);
        await using var nodeB = await StartNodeAsync(new(database, onB, InstanceId("node-b")), ct);

        // Act
        await nodeA.KillAsync();
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert — the dead run never reported, the survivor's run took a second attempt and recorded the one outcome.
        row.Status.Should().Be(JobStatus.Completed);
        row.AttemptCount.Should().Be(2);
        onB.CountFor(jobId).Should().Be(1);
        onA.CancelledAt(jobId).Should().BeNull();
    }

    [Fact]
    public async Task Recovered_job_that_fails_with_attempts_left_is_scheduled_to_retry()
    {
        // Arrange — the survivor's backoff is long, so the retry is still waiting when the test looks.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var onA = new ProbeInvocations();
        var onB = new ProbeInvocations();
        var nodeA = await StartNodeAsync(new(database, onA, InstanceId("node-a")), ct);
        var jobId = await nodeA.EnqueueAsync(Probe(ProbeBehaviours.BlockFirstThenThrow), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(onA.CountFor(jobId) == 1), Patience, ct);
        var survivorSettings = InstanceId("node-b");
        survivorSettings[ProbeKey("BackoffBaseSeconds")] = "600";
        await using var nodeB = await StartNodeAsync(new(database, onB, survivorSettings), ct);

        // Act
        await nodeA.KillAsync();
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Retrying, ct);

        // Assert — the recovery trigger carried no retry policy, so a trigger of the job's own waits to run it again.
        row.AttemptCount.Should().Be(2);
        onB.CountFor(jobId).Should().Be(1);
        (await JobsTestWait.UntilAsync(
            async () => await database.TriggerCountAsync(jobId, ct) == 1, TimeSpan.FromSeconds(10), ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Recovered_job_that_throws_while_its_node_stops_is_recovered_and_completes()
    {
        // Arrange — the survivor runs the recovered attempt until its own scheduler starts shutting down, which
        // refuses the trigger the next attempt needs.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        var nodeA = await StartNodeAsync(new(database, invocations, InstanceId("node-a")), ct);
        var jobId = await nodeA.EnqueueAsync(Probe(ProbeBehaviours.BlockFirstThenThrowWhileStopping), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 1), Patience, ct);
        var survivorSettings = InstanceId("node-b");
        survivorSettings["Endatix:BackgroundJobs:ShutdownWaitSeconds"] = "2";
        var nodeB = await StartNodeAsync(new(database, invocations, survivorSettings), ct);
        await nodeA.KillAsync();
        await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 2), Patience, ct);

        // Act
        await nodeB.StopAsync(ct);
        await nodeB.DisposeAsync();
        await using var restarted = await StartNodeAsync(new(database, invocations, survivorSettings), ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert — the stopping node left the firing for recovery, and the restarted node took the job over.
        row.Status.Should().Be(JobStatus.Completed);
        row.AttemptCount.Should().Be(3);
        invocations.CountFor(jobId).Should().Be(3);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Job_whose_outcome_cannot_be_written_runs_again_and_completes()
    {
        // Arrange — every try at recording the first attempt's completion fails, as while the database is away.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        var failures = new OutcomeWriteFailures(JobOutcomeRecorder.WriteRetryDelays.Length + 1);
        await using var node = JobsTestNode.Create(
            database.ConnectionString,
            configureServices: services =>
            {
                services.AddProbe(invocations);
                FailingOutcomeWrites.Register(services, failures);
            });
        await node.StartAsync(ct);

        // Act
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert — the trigger was kept, the next firing took the row over, and its completion was recorded.
        row.AttemptCount.Should().Be(2);
        invocations.CountFor(jobId).Should().Be(2);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Canceled_row_cancels_running_handler_on_another_node()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        await using var node = await StartNodeAsync(
            new(
                database,
                invocations,
                new Dictionary<string, string?> { ["Endatix:BackgroundJobs:CancellationPollSeconds"] = "1" }),
            ct);
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Block), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 1), Patience, ct);

        // Act — the write another node's cancel request makes.
        var canceledAt = DateTime.UtcNow;
        await database.ExecuteAsync($"""UPDATE jobs."BackgroundJobs" SET "Status" = 6 WHERE "Id" = {jobId}""", ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CancelledAt(jobId) is not null), TimeSpan.FromSeconds(5), ct);
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        // Assert
        invocations.CancelledAt(jobId).Should().NotBeNull();
        (invocations.CancelledAt(jobId)!.Value - canceledAt).Should().BeLessThan(TimeSpan.FromSeconds(2));
        var row = await database.ReadJobAsync(jobId, ct);
        row!.Status.Should().Be(JobStatus.Canceled);
        row.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task Wedged_handler_is_stopped_by_max_runtime()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartNodeAsync(
            new(
                database,
                new ProbeInvocations(),
                new Dictionary<string, string?> { [ProbeKey("MaxRuntimeMinutes")] = "1" }),
            ct);
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Block), ct);

        // Act
        await Task.Delay(TimeSpan.FromSeconds(70), ct);

        // Assert
        var row = await database.ReadJobAsync(jobId, ct);
        row!.Status.Should().BeOneOf(JobStatus.Retrying, JobStatus.DeadLettered);
    }

    [Fact]
    public async Task Execution_activity_parents_onto_enqueue_trace()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        using var testSource = new ActivitySource("Endatix.IntegrationTests.Jobs");
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Endatix.Jobs" or "Endatix.IntegrationTests.Jobs",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        await using var node = await StartNodeAsync(new(database, invocations), ct);
        long jobId;
        string traceId;

        // Act
        using (var request = testSource.StartActivity("request"))
        {
            traceId = request!.TraceId.ToString();
            jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);
        }

        await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert
        var run = invocations.Runs.Single(run => run.Job.JobId == jobId);
        run.TraceId.Should().Be(traceId);
        run.ActivitySource.Should().Be("Endatix.Jobs");
    }

    [Fact]
    public async Task Graceful_shutdown_records_nothing_and_job_reruns()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        var settings = new Dictionary<string, string?>
        {
            ["Endatix:BackgroundJobs:Clustering:InstanceId"] = "node-graceful",
            ["Endatix:BackgroundJobs:ShutdownWaitSeconds"] = "1",
        };
        var first = await StartNodeAsync(new(database, invocations, settings), ct);
        var jobId = await first.EnqueueAsync(Probe(ProbeBehaviours.BlockFirst), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 1), Patience, ct);

        // Act
        await first.StopAsync(ct);
        await first.DisposeAsync();
        var afterStop = await database.ReadJobAsync(jobId, ct);
        await using var restarted = await StartNodeAsync(new(database, invocations, settings), ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert
        afterStop!.Status.Should().Be(JobStatus.Processing);
        afterStop.AttemptCount.Should().Be(1);
        afterStop.ErrorMessage.Should().BeNull();
        row.Status.Should().Be(JobStatus.Completed);
        row.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task Lifecycle_metrics_are_recorded_per_outcome()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        using var lifecycle = JobLifecycleEventListener.Start();
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var node = await StartNodeAsync(new(database, new ProbeInvocations()), ct);

        // Act
        var succeeded = await node.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);
        var failed = await node.EnqueueAsync(Probe(ProbeBehaviours.Fail), ct);
        await database.WaitForStatusAsync(succeeded, JobStatus.Completed, ct);
        await database.WaitForStatusAsync(failed, JobStatus.Failed, ct);
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        // Assert
        var recorded = lifecycle.Events;
        recorded.Should().OnlyContain(entry => entry.JobType == ProbePayload.JobType);
        recorded.GroupBy(entry => entry.Event).ToDictionary(group => group.Key, group => group.Count())
            .Should().BeEquivalentTo(new Dictionary<string, int>
            {
                ["enqueued"] = 2,
                ["claimed"] = 2,
                ["completed"] = 1,
                ["failed"] = 1,
            });
    }

    private static BackgroundJobRequest Probe(string behaviour) =>
        BackgroundJobRequest.Create(new ProbePayload(behaviour), TenantId);

    private static string ProbeKey(string option) =>
        $"Endatix:BackgroundJobs:JobTypes:{ProbePayload.JobType}:{option}";

    private static Dictionary<string, string?> InstanceId(string instanceId) =>
        new() { ["Endatix:BackgroundJobs:Clustering:InstanceId"] = instanceId };

    private static async Task<JobsTestNode> StartNodeAsync(ProbeNodeSetup setup, CancellationToken ct)
    {
        var node = JobsTestNode.Create(
            setup.Database.ConnectionString,
            setup.Settings,
            services => services.AddProbe(setup.Invocations));
        await node.StartAsync(ct);
        return node;
    }

    // The scheduler deletes a finished one-off trigger just after the wrapper records the outcome.
    private static Task<bool> NoTriggerLeftAsync(JobsTestDatabase database, long jobId, CancellationToken ct) =>
        JobsTestWait.UntilAsync(async () => await database.TriggerCountAsync(jobId, ct) == 0, TimeSpan.FromSeconds(10), ct);

    /// <summary>A node running the probe handler against a test database, with any settings of its own.</summary>
    private sealed record ProbeNodeSetup(
        JobsTestDatabase Database,
        ProbeInvocations Invocations,
        Dictionary<string, string?>? Settings = null);
}
