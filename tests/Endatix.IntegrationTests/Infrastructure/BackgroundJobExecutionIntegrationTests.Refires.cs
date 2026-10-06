using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;

namespace Endatix.IntegrationTests;

/// <summary>
/// Re-fires the scheduler does not store: one made after the node stopped, which the next node recovers, and one the
/// scheduler keeps rejecting, which ends the job rather than hold its worker.
/// </summary>
public sealed partial class BackgroundJobExecutionIntegrationTests
{
    [Fact]
    public async Task Job_whose_outcome_cannot_be_written_after_its_node_stopped_is_recovered_by_another_node()
    {
        // Arrange — node A's handler finishes only once A has stopped waiting for it, and the outcome write fails, so
        // the firing gives up on re-firing the job after A's scheduler has let go of it.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var onA = new ProbeInvocations();
        var onB = new ProbeInvocations();
        var failures = new StateRepositoryFailures(completions: 1);
        var nodeA = await StartNodeFailingCompletionsAsync(new(database, onA, StoppingQuickly("node-a")), failures, ct);
        var jobId = await nodeA.EnqueueAsync(Probe(ProbeBehaviours.FinishFirstAsNodeStops), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(onA.CountFor(jobId) == 1), Patience, ct);
        await using var nodeB = await StartNodeAsync(new(database, onB, InstanceId("node-b")), ct);

        // Act
        await nodeA.StopAsync(ct);
        var writeTried = await JobsTestWait.UntilAsync(
            () => Task.FromResult(failures.CompletionsRemaining == 0), Patience, ct);
        await nodeA.DisposeAsync();
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert — the stopped scheduler kept the firing's trigger, so node B recovered it and ran it to the end.
        writeTried.Should().BeTrue();
        row.AttemptCount.Should().Be(2);
        onB.CountFor(jobId).Should().Be(1);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Job_whose_refire_the_scheduler_keeps_rejecting_is_dead_lettered_and_frees_its_worker()
    {
        // Arrange — the first attempt throws, and its retry's trigger and every re-fire after it are rejected; the
        // worker runs one job of the type at a time.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        var failures = TriggerStoreFailures.Rejected(stores: 1 + RefusedRefire.MaxRefusals);
        await using var scheduleOnly = await StartScheduleOnlyNodeAsync(database, FastAttempts(3), ct);
        var rejected = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.ThrowOnce), ct);
        await using var worker = await StartNodeFailingTriggerStoresAsync(
            new(database, invocations, FastAttempts(3)), failures, ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(rejected) == 1), Patience, ct);

        // Act
        var next = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);
        var row = await database.WaitForStatusAsync(new ExpectedJobStatus(rejected, JobStatus.DeadLettered, RetryPatience), ct);
        var nextRow = await database.WaitForStatusAsync(next, JobStatus.Completed, ct);

        // Assert — the job ended with a safe message, and the worker it held ran the next job.
        row.AttemptCount.Should().Be(1);
        row.ErrorMessage.Should().Be(BackgroundJobMessages.RefireRefused);
        invocations.CountFor(rejected).Should().Be(1);
        failures.Remaining.Should().Be(0);
        nextRow.Status.Should().Be(JobStatus.Completed);
        (await NoTriggerLeftAsync(database, rejected, ct)).Should().BeTrue();
    }

    private static Dictionary<string, string?> StoppingQuickly(string instanceId) => new(InstanceId(instanceId))
    {
        ["Endatix:BackgroundJobs:ShutdownWaitSeconds"] = "1",
    };

    private static async Task<JobsTestNode> StartNodeFailingCompletionsAsync(
        ProbeNodeSetup setup,
        StateRepositoryFailures failures,
        CancellationToken ct)
    {
        var node = JobsTestNode.Create(
            setup.Database.ConnectionString,
            setup.Settings,
            services =>
            {
                services.AddProbe(setup.Invocations);
                FailingStateRepository.Register(services, failures);
            });
        await node.StartAsync(ct);
        return node;
    }
}
