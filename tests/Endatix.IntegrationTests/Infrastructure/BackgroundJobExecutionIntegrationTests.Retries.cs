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
}
