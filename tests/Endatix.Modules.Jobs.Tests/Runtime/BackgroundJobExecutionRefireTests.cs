using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// The job wrapper when a firing that is the job's only trigger cannot settle the row: it re-fires the job, and
/// while the scheduler stops it holds the firing for recovery.
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
        scheduled[1].RetryPolicy.Should().NotBeNull();
        await repository.DidNotReceiveWithAnyArgs().RecordFailedAttemptAsync(default, default!, default);
        provider.GetRequiredService<IJobMetrics>().DidNotReceive()
            .Record(JobLifecycleEvent.RetryScheduled, Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_RecoveredRunThrowsAndNoTriggerCanBeScheduled_WritesNothingAndReturns()
    {
        // Arrange
        var repository = RecoveringRepository(attempt: 2);
        await using var provider = Services(repository, new ObservedRun { Throw = true });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        RefuseEveryTrigger(context.Scheduler);

        // Act
        var act = async () => await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        await context.Scheduler.Received(2)
            .ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().RecordFailedAttemptAsync(default, default!, default);
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

    private static IBackgroundJobStateRepository RecoveringRepository(int attempt)
    {
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(ClaimOfJob(recovering: true), Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, ProbeJobType, TenantA, "{}", attempt, null, JobStatus.Processing));
        return repository;
    }

    private static void RefuseEveryTrigger(IScheduler scheduler) =>
        scheduler
            .ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<DateTimeOffset>>(_ => throw new SchedulerException("The Scheduler has been Shutdown."));

    private static ValueTask<DateTimeOffset> Stored() => new(DateTimeOffset.UtcNow);

    private static bool IsReclaim(ITrigger trigger) =>
        trigger.JobDataMap.GetString(BackgroundJobExecution.ReclaimKey) == bool.TrueString;
}
