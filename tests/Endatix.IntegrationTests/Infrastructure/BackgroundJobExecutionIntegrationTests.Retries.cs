using System.Globalization;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// Retries: each one runs on a trigger the job wrapper stores for the job, judged against the attempt budget of the
/// node that runs the attempt, whatever the trigger was stored with.
/// </summary>
public sealed partial class BackgroundJobExecutionIntegrationTests
{
    private static readonly TimeSpan RetryPatience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Job_enqueued_with_two_attempts_and_run_with_four_is_dead_lettered_at_attempt_four()
    {
        // Arrange — the node that enqueues allows two attempts; the node started afterwards to run the job, four.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        await using var scheduleOnly = await StartScheduleOnlyNodeAsync(database, FastAttempts(2), ct);
        var jobId = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.Throw), ct);

        // Act
        await using var worker = await StartNodeAsync(new(database, invocations, FastAttempts(4)), ct);
        var row = await database.WaitForStatusAsync(new ExpectedJobStatus(jobId, JobStatus.DeadLettered, RetryPatience), ct);

        // Assert
        row.Status.Should().Be(JobStatus.DeadLettered);
        row.AttemptCount.Should().Be(4);
        invocations.CountFor(jobId).Should().Be(4);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Job_whose_claim_fails_once_uses_all_its_attempts_then_is_dead_lettered()
    {
        // Arrange — the first claim throws, as it would while the database is away.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        await using var node = JobsTestNode.Create(
            database.ConnectionString,
            FastAttempts(3),
            services =>
            {
                services.AddProbe(invocations);
                FailingStateRepository.Register(services, new StateRepositoryFailures(claims: 1));
            });
        await node.StartAsync(ct);

        // Act
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Throw), ct);
        var row = await database.WaitForStatusAsync(new ExpectedJobStatus(jobId, JobStatus.DeadLettered, RetryPatience), ct);

        // Assert
        row.Status.Should().Be(JobStatus.DeadLettered);
        row.AttemptCount.Should().Be(3);
        invocations.CountFor(jobId).Should().Be(3);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Job_on_a_trigger_stored_with_a_retry_policy_is_retried_until_its_attempts_run_out()
    {
        // Arrange — the trigger carries a retry policy of one retry, as earlier versions stored it.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        await using var scheduleOnly = await StartScheduleOnlyNodeAsync(database, FastAttempts(3), ct);
        var jobId = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.Throw), ct);
        await GiveTriggerRetryPolicyAsync(scheduleOnly, jobId, ct);

        // Act
        await using var worker = await StartNodeAsync(new(database, invocations, FastAttempts(3)), ct);
        var row = await database.WaitForStatusAsync(new ExpectedJobStatus(jobId, JobStatus.DeadLettered, RetryPatience), ct);

        // Assert — every attempt ran once, and nothing was left behind for the scheduler's policy to fire.
        row.Status.Should().Be(JobStatus.DeadLettered);
        row.AttemptCount.Should().Be(3);
        invocations.CountFor(jobId).Should().Be(3);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Retry_of_a_job_on_a_trigger_stored_with_a_retry_policy_waits_on_a_trigger_without_one()
    {
        // Arrange — a backoff long enough that the retry is still waiting when the test looks.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var scheduleOnly = await StartScheduleOnlyNodeAsync(database, FastAttempts(3), ct);
        var jobId = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.Throw), ct);
        await GiveTriggerRetryPolicyAsync(scheduleOnly, jobId, ct);
        var stored = await database.ReadTriggerAsync(jobId, ct);
        var settings = FastAttempts(3);
        settings[ProbeKey("BackoffBaseSeconds")] = "600";

        // Act
        await using var worker = await StartNodeAsync(new(database, new ProbeInvocations(), settings), ct);
        var row = await database.WaitForStatusAsync(jobId, JobStatus.Retrying, ct);
        var retry = await database.ReadTriggerAsync(jobId, ct);

        // Assert — the job's one trigger now has no policy, and fires when the row says the next attempt is due.
        stored!.RetryPolicy.Should().NotBeNull();
        row.AttemptCount.Should().Be(1);
        retry!.RetryPolicy.Should().BeNull();
        retry.NextFireTime.Should().BeCloseTo(row.NextAttemptAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Job_whose_scheduler_retries_run_out_while_its_row_is_unfinished_is_taken_over_and_completes()
    {
        // Arrange — the job's only trigger is one whose firings throw to the scheduler, with one retry, so the
        // scheduler gives up on it and deletes it while the row is still Pending.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        await using var scheduleOnly = await StartScheduleOnlyNodeAsync(database, new(), ct);
        var jobId = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.Succeed), ct);
        await ReplaceWithThrowingTriggerAsync(scheduleOnly, jobId, ct);

        // Act
        await using var worker = await StartNodeAsync(new(database, invocations), ct);
        var row = await database.WaitForStatusAsync(new ExpectedJobStatus(jobId, JobStatus.Completed, RetryPatience), ct);

        // Assert — a trigger of the job's own took it over and ran it.
        row.Status.Should().Be(JobStatus.Completed);
        row.AttemptCount.Should().Be(1);
        invocations.CountFor(jobId).Should().Be(1);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Job_whose_trigger_stores_fail_for_a_while_is_still_retried_and_completes()
    {
        // Arrange — the retry's trigger and the first three tries of the re-fire that replaces it cannot be stored.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        var failures = TriggerStoreFailures.DatabaseDown(stores: 4);
        await using var scheduleOnly = await StartScheduleOnlyNodeAsync(database, FastAttempts(3), ct);
        var jobId = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.ThrowOnce), ct);

        // Act
        await using var worker = await StartNodeFailingTriggerStoresAsync(
            new(database, invocations, FastAttempts(3)), failures, ct);
        var row = await database.WaitForStatusAsync(new ExpectedJobStatus(jobId, JobStatus.Completed, RetryPatience), ct);

        // Assert — the firing stayed open until a trigger was stored, and that trigger ran the next attempt.
        row.Status.Should().Be(JobStatus.Completed);
        row.AttemptCount.Should().Be(2);
        invocations.CountFor(jobId).Should().Be(2);
        failures.Remaining.Should().Be(0);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Job_whose_trigger_stores_fail_for_a_while_is_dead_lettered_at_its_last_attempt()
    {
        // Arrange — the handler always throws, and the first attempt's retry and re-fire cannot be stored for a while.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var invocations = new ProbeInvocations();
        var failures = TriggerStoreFailures.DatabaseDown(stores: 4);
        await using var scheduleOnly = await StartScheduleOnlyNodeAsync(database, FastAttempts(2), ct);
        var jobId = await scheduleOnly.EnqueueAsync(Probe(ProbeBehaviours.Throw), ct);

        // Act
        await using var worker = await StartNodeFailingTriggerStoresAsync(
            new(database, invocations, FastAttempts(2)), failures, ct);
        var row = await database.WaitForStatusAsync(new ExpectedJobStatus(jobId, JobStatus.DeadLettered, RetryPatience), ct);

        // Assert
        row.Status.Should().Be(JobStatus.DeadLettered);
        row.AttemptCount.Should().Be(2);
        invocations.CountFor(jobId).Should().Be(2);
        failures.Remaining.Should().Be(0);
        (await NoTriggerLeftAsync(database, jobId, ct)).Should().BeTrue();
    }

    private static Dictionary<string, string?> FastAttempts(int maxAttempts) => new()
    {
        [ProbeKey("MaxAttempts")] = maxAttempts.ToString(CultureInfo.InvariantCulture),
        [ProbeKey("BackoffBaseSeconds")] = "1",
    };

    private static async Task<JobsTestNode> StartScheduleOnlyNodeAsync(
        JobsTestDatabase database,
        Dictionary<string, string?> settings,
        CancellationToken ct)
    {
        var node = JobsTestNode.Create(
            database.ConnectionString,
            new Dictionary<string, string?>(settings) { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await node.StartAsync(ct);
        return node;
    }

    // Only this node's stores spend the failures: the tests enqueue from another node, whose stores never fail.
    private static async Task<JobsTestNode> StartNodeFailingTriggerStoresAsync(
        ProbeNodeSetup setup,
        TriggerStoreFailures failures,
        CancellationToken ct)
    {
        var node = JobsTestNode.Create(
            setup.Database.ConnectionString,
            setup.Settings,
            services =>
            {
                services.AddProbe(setup.Invocations);
                FailingTriggerStoreDelegate.Register(services, failures);
            });
        await node.StartAsync(ct);
        return node;
    }

    private static Task<IScheduler> SchedulerOfAsync(JobsTestNode node, CancellationToken ct) =>
        node.Services.GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName).GetScheduler(ct).AsTask();

    // The job's trigger as earlier versions stored it: the same trigger, with a retry policy of one retry.
    private static async Task GiveTriggerRetryPolicyAsync(JobsTestNode node, long jobId, CancellationToken ct)
    {
        var scheduler = await SchedulerOfAsync(node, ct);
        var key = new TriggerKey(jobId.ToString(CultureInfo.InvariantCulture), ProbePayload.JobType);
        var trigger = (await scheduler.GetTrigger(key, ct))!.GetTriggerBuilder()
            .WithRetryPolicy(RetryPolicy.Fixed(1, TimeSpan.FromSeconds(1)))
            .StartNow()
            .Build();
        await scheduler.RescheduleJob(key, trigger, ct);
    }

    private static async Task ReplaceWithThrowingTriggerAsync(JobsTestNode node, long jobId, CancellationToken ct)
    {
        var scheduler = await SchedulerOfAsync(node, ct);
        var id = jobId.ToString(CultureInfo.InvariantCulture);
        await scheduler.UnscheduleJob(new TriggerKey(id, ProbePayload.JobType), ct);

        // Named after the job type, as the job wrapper's durable job is, because the job type is read from the name.
        var throwing = JobBuilder.Create<ThrowingJob>().WithIdentity(ProbePayload.JobType, "throwing").Build();
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"throwing-{id}", ProbePayload.JobType)
            .ForJob(throwing)
            .WithExecutionGroup(ProbePayload.JobType)
            .UsingJobData(BackgroundJobExecution.JobIdKey, id)
            .WithRetryPolicy(RetryPolicy.Fixed(1, TimeSpan.FromSeconds(1)))
            .StartNow()
            .Build();
        await scheduler.ScheduleJob(throwing, trigger, default, ct);
    }
}
