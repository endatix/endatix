using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// A re-fire the running scheduler refuses for a reason of its own, which trying again does not change: the job is
/// dead-lettered after a few tries rather than holding its worker, and a durable job that has gone is stored again.
/// </summary>
public sealed partial class BackgroundJobExecutionRefireTests
{
    [Fact]
    public async Task Execute_SchedulerKeepsRejectingRefire_DeadLettersAtClaimedAttemptAndCompletes()
    {
        // Arrange — the retry's trigger and every re-fire are rejected; the durable job is in the store.
        var repository = RecoveringRepository(attempt: 2);
        repository.TryDeadLetterAsync(Arg.Any<UnfinishedRow>(), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, new ObservedRun { Throw = true });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        RejectEveryTrigger(context.Scheduler);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert — the retry's one try, then the re-fire's tries up to the limit, and the row dead-lettered at its claim.
        ScheduleJobCalls(context.Scheduler).Should().Be(1 + RefusedRefire.MaxRefusals);
        await repository.Received(1).TryDeadLetterAsync(
            new UnfinishedRow(JobId, JobStatus.Processing, 2),
            Arg.Is<AttemptFailure>(failure => failure.ErrorMessage == BackgroundJobMessages.RefireRefused),
            Arg.Any<CancellationToken>());
        provider.GetRequiredService<IJobMetrics>().Received(1).Record(JobLifecycleEvent.DeadLettered, ProbeJobType);
    }

    [Fact]
    public async Task Execute_ClaimFailsAndRefireRejected_DeadLettersTheRowAsItReadsIt()
    {
        // Arrange — the firing holds no claim, so the write is fenced on the row as it is read.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryClaimAsync(ClaimOfJob(recovering: true), Arg.Any<CancellationToken>())
            .Returns<Task<ClaimedJob?>>(_ => throw new TimeoutException("The database did not answer."));
        repository.ReadAttemptAsync(JobId, Arg.Any<CancellationToken>())
            .Returns(new JobAttemptState(JobStatus.Processing, 1));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        RejectEveryTrigger(context.Scheduler);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        ScheduleJobCalls(context.Scheduler).Should().Be(RefusedRefire.MaxRefusals);
        await repository.Received(1).TryDeadLetterAsync(
            new UnfinishedRow(JobId, JobStatus.Processing, 1), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_RefireRejectedAndRowAlreadyFinished_CompletesWithoutWriting()
    {
        // Arrange
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryClaimAsync(ClaimOfJob(recovering: true), Arg.Any<CancellationToken>())
            .Returns<Task<ClaimedJob?>>(_ => throw new TimeoutException("The database did not answer."));
        repository.ReadAttemptAsync(JobId, Arg.Any<CancellationToken>())
            .Returns(new JobAttemptState(JobStatus.Completed, 1));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        RejectEveryTrigger(context.Scheduler);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        await repository.DidNotReceiveWithAnyArgs().TryDeadLetterAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Execute_DeadLetterOfRejectedRefireFails_HoldsFiringUntilShutdownSignal()
    {
        // Arrange
        var repository = RecoveringRepository(attempt: 2);
        repository.TryDeadLetterAsync(Arg.Any<UnfinishedRow>(), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun { Throw = true });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        RejectEveryTrigger(context.Scheduler);

        // Act
        var executing = execution.Execute(context, TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        var openUntilSignal = !executing.IsCompleted;
        provider.GetRequiredService<JobsShutdownSignal>().Raise();
        await executing.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert — the trigger and the dead-letter were both tried past the limit, as a store that is down would be.
        openUntilSignal.Should().BeTrue();
        ScheduleJobCalls(context.Scheduler).Should().BeGreaterThan(1 + RefusedRefire.MaxRefusals);
        repository.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IBackgroundJobStateRepository.TryDeadLetterAsync))
            .Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task Execute_RefireRejectedBecauseDurableJobIsMissing_StoresTheJobAgainThenTheRefire()
    {
        // Arrange — the claim fails, so the re-fire is the only trigger stored.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryClaimAsync(ClaimOfJob(recovering: true), Arg.Any<CancellationToken>())
            .Returns<Task<ClaimedJob?>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        var jobKey = QuartzRegistration.JobKeyFor(ProbeJobType);
        context.Scheduler.Exists(jobKey, Arg.Any<CancellationToken>()).Returns(false);
        context.Scheduler
            .ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw JobMissing(jobKey), _ => Stored());

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        await context.Scheduler.Received(1).AddJob(
            Arg.Is<IJobDetail>(job => job.Key.Equals(jobKey) && job.Durable), AddJobOptions.Replacing, Arg.Any<CancellationToken>());
        ScheduleJobCalls(context.Scheduler).Should().Be(2);
        await repository.DidNotReceiveWithAnyArgs().TryDeadLetterAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Execute_ClaimFailsAndItsTriggerIsGone_StoresTheRefireAsANewTrigger()
    {
        // Arrange — the trigger that fired was deleted meanwhile, so there is nothing to reschedule.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryClaimAsync(ClaimOfJob(recovering: false), Arg.Any<CancellationToken>())
            .Returns<Task<ClaimedJob?>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);
        context.Scheduler
            .RescheduleJob(context.Trigger.Key, Arg.Any<ITrigger>(), Arg.Any<CancellationToken>())
            .Returns((DateTimeOffset?)null);
        var scheduled = CaptureStoredTriggers(context.Scheduler);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        scheduled.Should().ContainSingle().Which.Key.Should().Be(context.Trigger.Key);
        IsReclaim(scheduled[0]).Should().BeTrue();
    }

    private static void RejectEveryTrigger(IScheduler scheduler)
    {
        scheduler.Exists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(true);
        scheduler
            .ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<DateTimeOffset>>(_ => throw new SchedulerException("The trigger could not be stored."));
    }

    // As the scheduler words it when the job a trigger points at is not in the store.
    private static JobPersistenceException JobMissing(JobKey jobKey) =>
        new($"The job ({jobKey}) referenced by the trigger does not exist.");

    private static int ScheduleJobCalls(IScheduler scheduler) =>
        scheduler.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IScheduler.ScheduleJob));
}
