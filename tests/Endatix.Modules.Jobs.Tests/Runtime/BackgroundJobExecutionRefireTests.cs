using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// The job wrapper when a firing that is the job's only trigger cannot settle the row: it re-fires the job, trying
/// until the re-fire is stored, and while the scheduler stops it holds the firing for recovery.
/// </summary>
public sealed class BackgroundJobExecutionRefireTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Execute_RecoveredRunThrowsAndOwnTriggerCannotBeScheduled_RefiresToReclaimWithoutRecordingRetry()
    {
        // Arrange
        var repository = RecoveringRepository(attempt: 2);
        await using var provider = Services(repository, new ObservedRun { Throw = true });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        var scheduled = new List<ITrigger>();
        context.Scheduler
            .ScheduleJob(Arg.Do<ITrigger>(scheduled.Add), Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new JobPersistenceException("The database did not answer."), _ => Stored());

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        scheduled.Should().HaveCount(2);
        IsReclaim(scheduled[1]).Should().BeTrue();
        scheduled[1].Key.Should().Be(new TriggerKey(JobId.ToString(), ProbeJobType));
        scheduled[1].RetryPolicy.Should().BeNull();
        await repository.DidNotReceiveWithAnyArgs().RecordFailedAttemptAsync(default, default!, default);
        provider.GetRequiredService<IJobMetrics>().DidNotReceive()
            .Record(JobLifecycleEvent.RetryScheduled, Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_RunningSchedulerRefusesEveryTrigger_KeepsFiringOpenUntilShutdownSignal()
    {
        // Arrange — completing the firing would delete the job's only trigger and leave its row Processing.
        var repository = RecoveringRepository(attempt: 2);
        await using var provider = Services(repository, new ObservedRun { Throw = true });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        RefuseEveryTrigger(context.Scheduler);
        context.Scheduler.Status.Returns(SchedulerStatus.Running);

        // Act
        var executing = execution.Execute(context, TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        var openUntilSignal = !executing.IsCompleted;
        provider.GetRequiredService<JobsShutdownSignal>().Raise();
        await executing.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert — it kept trying the reclaim trigger, beyond the retry's and the reclaim's first tries.
        openUntilSignal.Should().BeTrue();
        executing.IsCompletedSuccessfully.Should().BeTrue();
        context.Scheduler.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IScheduler.ScheduleJob))
            .Should().BeGreaterThan(2);
        await repository.DidNotReceiveWithAnyArgs().RecordFailedAttemptAsync(default, default!, default);
    }

    [Fact]
    public async Task Execute_ReclaimTriggerRefusedTwiceThenStored_CompletesWithReclaimTriggerTimedFromItsLastTry()
    {
        // Arrange — the retry's trigger and the reclaim's first two tries are refused; the clock moves on each read.
        await using var provider = Services(RecoveringRepository(attempt: 2), new ObservedRun { Throw = true });
        var start = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var reads = 0;
        provider.GetRequiredService<IDateTimeProvider>().UtcNow.Returns(_ => start.AddSeconds(++reads));
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        var scheduled = new List<ITrigger>();
        context.Scheduler
            .ScheduleJob(Arg.Do<ITrigger>(scheduled.Add), Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw Refused(), _ => throw Refused(), _ => throw Refused(), _ => Stored());

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert — each try is timed from when it was made, so the stored one is the latest.
        scheduled.Should().HaveCount(4);
        var reclaims = scheduled.Skip(1).ToList();
        reclaims.Should().OnlyContain(trigger => IsReclaim(trigger));
        reclaims.Select(trigger => trigger.StartTimeUtc).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Execute_SchedulerShuttingDownWhenRetryCannotBeScheduled_HoldsFiringUntilShutdownSignal()
    {
        // Arrange
        await using var provider = Services(RecoveringRepository(attempt: 2), new ObservedRun { Throw = true });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        RefuseEveryTrigger(context.Scheduler);
        context.Scheduler.Status.Returns(SchedulerStatus.ShuttingDown);

        // Act
        var executing = execution.Execute(context, TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(UnrecordedJobRefire.StopPollInterval * 4, TestContext.Current.CancellationToken);
        var heldUntilSignal = !executing.IsCompleted;
        provider.GetRequiredService<JobsShutdownSignal>().Raise();
        await executing.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        heldUntilSignal.Should().BeTrue();
        executing.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_SchedulerShutDownWhenRefireCannotBeScheduled_ReturnsWithoutShutdownSignal()
    {
        // Arrange — a host torn down without stopping shuts its scheduler down and never raises the signal.
        await using var provider = Services(RecoveringRepository(attempt: 2), new ObservedRun { Throw = true });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        RefuseEveryTrigger(context.Scheduler);
        context.Scheduler.Status.Returns(SchedulerStatus.Shutdown);

        // Act
        var executing = execution.Execute(context, TestContext.Current.CancellationToken).AsTask();
        await executing.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        executing.IsCompletedSuccessfully.Should().BeTrue();
        provider.GetRequiredService<JobsShutdownSignal>().IsRaised.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_ClaimFailsOnRecoveryFiring_RefiresToReclaimWithoutRunningHandler()
    {
        // Arrange
        var observed = new ObservedRun();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryClaimAsync(ClaimOfJob(recovering: true), Arg.Any<CancellationToken>())
            .Returns<Task<ClaimedJob?>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        var scheduled = CaptureStoredTriggers(context.Scheduler);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        observed.ContextTenantId.Should().Be(-1);
        scheduled.Should().ContainSingle().Which.Should().Match<ITrigger>(trigger => IsReclaim(trigger));
        scheduled[0].RetryPolicy.Should().BeNull();
    }

    [Fact]
    public async Task Execute_DeadLetterFailsOnRecoveryFiring_RefiresToReclaim()
    {
        // Arrange
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryDeadLetterSpentAsync(Arg.Any<AttemptRef>(), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        var scheduled = CaptureStoredTriggers(context.Scheduler);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        scheduled.Should().ContainSingle().Which.Should().Match<ITrigger>(trigger => IsReclaim(trigger));
        await repository.DidNotReceiveWithAnyArgs().TryClaimAsync(default!, default);
    }

    [Fact]
    public async Task Execute_ClaimFailsOnJobTriggerFiring_ReschedulesItsTriggerToReclaimWithoutThrowing()
    {
        // Arrange — the claim may have landed before it threw, so the next firing has to be able to take the row.
        var observed = new ObservedRun();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryClaimAsync(ClaimOfJob(recovering: false), Arg.Any<CancellationToken>())
            .Returns<Task<ClaimedJob?>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = LegacyJobTriggerFiringOf(JobId);
        ITrigger? rescheduled = null;
        context.Scheduler
            .RescheduleJob(context.Trigger.Key, Arg.Do<ITrigger>(trigger => rescheduled = trigger), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.UtcNow);

        // Act
        var act = async () => await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert — nothing is thrown for a retry policy to act on, and the job's trigger fires again to re-claim it.
        await act.Should().NotThrowAsync();
        observed.ContextTenantId.Should().Be(-1);
        rescheduled.Should().NotBeNull();
        IsReclaim(rescheduled).Should().BeTrue();
        rescheduled.RetryPolicy.Should().BeNull();
    }

    private static IBackgroundJobStateRepository RecoveringRepository(int attempt)
    {
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(ClaimOfJob(recovering: true), Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, ProbeJobType, TenantA, "{}", attempt, null, JobStatus.Processing));
        return repository;
    }

    private static List<ITrigger> CaptureStoredTriggers(IScheduler scheduler)
    {
        var scheduled = new List<ITrigger>();
        scheduler
            .ScheduleJob(Arg.Do<ITrigger>(scheduled.Add), Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => Stored());
        return scheduled;
    }

    private static void RefuseEveryTrigger(IScheduler scheduler) =>
        scheduler
            .ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<DateTimeOffset>>(_ => throw Refused());

    private static SchedulerException Refused() => new("The trigger could not be stored.");

    private static ValueTask<DateTimeOffset> Stored() => new(DateTimeOffset.UtcNow);

    private static bool IsReclaim(ITrigger trigger) =>
        trigger.JobDataMap.GetString(BackgroundJobExecution.ReclaimKey) == bool.TrueString;
}
