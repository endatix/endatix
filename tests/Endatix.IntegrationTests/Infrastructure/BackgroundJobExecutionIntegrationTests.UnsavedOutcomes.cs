using System.Globalization;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// An attempt whose outcome cannot be written while the database is away: the outcome its handler decided is recorded
/// by the job's re-fire, without running the job again, and a throw is still judged by re-claiming the row.
/// </summary>
public sealed partial class BackgroundJobExecutionIntegrationTests
{
    // Every try the first firing makes at its outcome write.
    private static readonly int OneFiringOfWrites = JobOutcomeRecorder.WriteRetryDelays.Length + 1;

    [Fact]
    public async Task Last_attempt_that_succeeded_but_could_not_save_its_result_completes_once_the_database_is_back()
    {
        // Arrange — the first firing's writes all fail, and so do the re-fire's first two.
        var ct = TestContext.Current.CancellationToken;
        using var lifecycle = JobLifecycleEventListener.Start();
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var invocations = new ProbeInvocations();
        var failures = new StateRepositoryFailures(completions: OneFiringOfWrites + 2);
        await using var node = await StartNodeFailingStateWritesAsync(new(database, invocations, OneAttempt()), failures, ct);

        // Act
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert — the handler ran once, and its end was counted once.
        row.AttemptCount.Should().Be(1);
        row.ErrorMessage.Should().BeNull();
        invocations.CountFor(jobId).Should().Be(1);
        failures.CompletionsRemaining.Should().Be(0);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
        CountOf(lifecycle, "completed").Should().Be(1);
        CountOf(lifecycle, "dead_lettered").Should().Be(0);
    }

    [Fact]
    public async Task Last_attempt_that_returned_a_failure_but_could_not_save_it_ends_failed_not_dead_lettered()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var invocations = new ProbeInvocations();
        var failures = new StateRepositoryFailures { Fails = OneFiringOfWrites + 1 };
        await using var node = await StartNodeFailingStateWritesAsync(new(database, invocations, OneAttempt()), failures, ct);

        // Act
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Fail), ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Failed, ct);

        // Assert
        row.ErrorMessage.Should().Be(ProbeBehaviours.FailureMessage);
        row.AttemptCount.Should().Be(1);
        invocations.CountFor(jobId).Should().Be(1);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Last_attempt_whose_completion_cannot_be_saved_or_refired_completes_instead_of_dead_lettering()
    {
        // Arrange — the first firing's writes all fail, and the scheduler refuses every re-fire carrying the completion.
        var ct = TestContext.Current.CancellationToken;
        using var lifecycle = JobLifecycleEventListener.Start();
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var invocations = new ProbeInvocations();
        var writeFailures = new StateRepositoryFailures(completions: OneFiringOfWrites);
        var refusals = TriggerStoreFailures.Rejected(stores: RefusedRefire.MaxRefusals);
        await using var scheduleOnly = await StartScheduleOnlyNodeAsync(database, OneAttempt(), ct);
        var jobId = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);

        // Act
        await using var worker = await StartNodeFailingAsync(
            new(database, invocations, OneAttempt()),
            services =>
            {
                FailingStateRepository.Register(services, writeFailures);
                FailingTriggerStoreDelegate.Register(services, database.Provider, refusals);
            },
            ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert — the firing wrote the completion itself once the re-fire was refused, and counted it once.
        row.AttemptCount.Should().Be(1);
        row.ErrorMessage.Should().BeNull();
        invocations.CountFor(jobId).Should().Be(1);
        writeFailures.CompletionsRemaining.Should().Be(0);
        refusals.Remaining.Should().Be(0);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
        CountOf(lifecycle, "completed").Should().Be(1);
        CountOf(lifecycle, "dead_lettered").Should().Be(0);
    }

    [Fact]
    public async Task Job_canceled_while_its_unsaved_completion_waits_stays_canceled()
    {
        // Arrange — the first firing's writes all fail, so its completion waits on the re-fire.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var invocations = new ProbeInvocations();
        var failures = new StateRepositoryFailures(completions: OneFiringOfWrites);
        await using var node = await StartNodeFailingStateWritesAsync(new(database, invocations, OneAttempt()), failures, ct);
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(failures.CompletionsRemaining == 0), Patience, ct);

        // Act — the write another node's cancel request makes, before the re-fire is due.
        await database.ExecuteAsync($"""UPDATE jobs."BackgroundJobs" SET "Status" = 6 WHERE "Id" = {jobId}""", ct);
        var refired = await JobsTestWait.UntilAsync(
            async () => await database.TriggerCountAsync(jobId, ct) == 0, Patience, ct);

        // Assert
        refired.Should().BeTrue();
        var row = await database.ReadJobAsync(jobId, ct);
        row!.Status.Should().Be(JobStatus.Canceled);
        row.CompletedAt.Should().BeNull();
        invocations.CountFor(jobId).Should().Be(1);
    }

    [Fact]
    public async Task Attempt_with_attempts_left_that_threw_and_could_not_save_its_retry_is_reclaimed_and_runs_again()
    {
        // Arrange — the retry's backoff is longer than the re-fire's delay, which replaces the retry's trigger.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var invocations = new ProbeInvocations();
        var failures = new StateRepositoryFailures { FailedAttempts = OneFiringOfWrites };
        var settings = new Dictionary<string, string?>
        {
            [ProbeKey("MaxAttempts")] = "3",
            [ProbeKey("BackoffBaseSeconds")] = "60",
        };
        await using var node = await StartNodeFailingStateWritesAsync(new(database, invocations, settings), failures, ct);

        // Act
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.ThrowOnce), ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Completed, ct);

        // Assert — nothing was carried for the throw; the re-fire re-claimed the row for the next attempt.
        row.AttemptCount.Should().Be(2);
        invocations.CountFor(jobId).Should().Be(2);
        failures.FailedAttempts.Should().Be(0);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Reclaim_trigger_without_a_carried_outcome_dead_letters_a_job_left_processing_on_its_last_attempt()
    {
        // Arrange — a re-fire as earlier versions stored it, for a last attempt that left its row Processing.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var invocations = new ProbeInvocations();
        await using var scheduleOnly = await StartScheduleOnlyNodeAsync(database, OneAttempt(), ct);
        var jobId = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);
        await database.ExecuteAsync(
            $"""UPDATE jobs."BackgroundJobs" SET "Status" = 1, "AttemptCount" = 1 WHERE "Id" = {jobId}""", ct);
        await ReplaceWithLegacyReclaimTriggerAsync(scheduleOnly, jobId, ct);

        // Act
        await using var worker = await StartNodeAsync(new(database, invocations, OneAttempt()), ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.DeadLettered, ct);

        // Assert
        row.ErrorMessage.Should().Be(BackgroundJobMessages.StoppedOnLastAttempt);
        row.AttemptCount.Should().Be(1);
        invocations.CountFor(jobId).Should().Be(0);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    private static Dictionary<string, string?> OneAttempt() => new() { [ProbeKey("MaxAttempts")] = "1" };

    private static async Task<JobsTestNode> StartNodeFailingAsync(
        ProbeNodeSetup setup,
        Action<IServiceCollection> registerFailures,
        CancellationToken ct)
    {
        var node = JobsTestNode.Create(
            setup.Database,
            setup.Settings,
            services =>
            {
                services.AddProbe(setup.Invocations);
                registerFailures(services);
            });
        await node.StartAsync(ct);
        return node;
    }

    private static int CountOf(JobLifecycleEventListener lifecycle, string lifecycleEvent) =>
        lifecycle.Events.Count(entry => entry.Event == lifecycleEvent && entry.JobType == ProbePayload.JobType);

    private static async Task ReplaceWithLegacyReclaimTriggerAsync(JobsTestNode node, long jobId, CancellationToken ct)
    {
        var scheduler = await node.Services
            .GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName).GetScheduler(ct);
        var key = new TriggerKey(jobId.ToString(CultureInfo.InvariantCulture), ProbePayload.JobType);
        var reclaim = QuartzRegistration.TriggerFor(new JobTriggerSpec(jobId, ProbePayload.JobType, Reclaim: true));
        await scheduler.RescheduleJob(key, reclaim, ct);
    }
}
