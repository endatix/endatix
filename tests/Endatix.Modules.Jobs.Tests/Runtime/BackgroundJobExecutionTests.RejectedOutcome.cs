using System.Data.Common;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// A carried outcome whose write the database refuses while it is up: each rejection is counted on the re-fire, and
/// once the outcome is spent it is dropped and the row re-claimed, so a write the database will never take ends.
/// </summary>
public sealed partial class BackgroundJobExecutionTests
{
    [Fact]
    public async Task Execute_CarriedOutcomeRejectedWhileDatabaseAnswers_RefiresCountingTheRejection()
    {
        // Arrange — the write breaks a rule of the table, and the row still reads.
        var repository = RejectingCompletions();
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var carried = Carried(AttemptRowWrite.Completed);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), carried);
        var rescheduled = CaptureRescheduledTriggers(context);

        // Act
        await ExecuteWithinPatienceAsync(execution, context);

        // Assert
        UnsavedOutcome.Read(rescheduled.Should().ContainSingle().Subject.JobDataMap).Outcome.Should()
            .Be(carried with { Rejections = 1 });
    }

    [Fact]
    public async Task Execute_CarriedOutcomeRejectedTheLastAllowedTime_RefiresToReclaimWithoutTheOutcome()
    {
        // Arrange
        var repository = RejectingCompletions();
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var carried = Carried(AttemptRowWrite.Completed) with { Rejections = UnsavedOutcome.MaxRejections - 1 };
        var context = FiringCarrying(JobTriggerFiringOf(JobId), carried);
        var rescheduled = CaptureRescheduledTriggers(context);

        // Act
        await ExecuteWithinPatienceAsync(execution, context);

        // Assert — the next firing re-claims the row the attempt left Processing, as one carrying nothing does.
        var refire = rescheduled.Should().ContainSingle().Subject;
        refire.JobDataMap.GetString(BackgroundJobExecution.ReclaimKey).Should().Be(bool.TrueString);
        refire.JobDataMap.ContainsKey(UnsavedOutcome.DataKey).Should().BeFalse();
        provider.GetRequiredService<IJobMetrics>().DidNotReceive().Record(JobLifecycleEvent.Completed, Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_CarriedOutcomeWriteAndRowReadBothFail_RefiresWithoutCountingARejection()
    {
        // Arrange — the driver does not mark the failure transient, and the database does not answer the read either.
        var repository = RejectingCompletions();
        repository.ReadAttemptAsync(JobId, Arg.Any<CancellationToken>())
            .Returns<Task<JobAttemptState?>>(_ => throw new InvalidOperationException("The connection was reset."));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var carried = Carried(AttemptRowWrite.Completed);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), carried);
        var rescheduled = CaptureRescheduledTriggers(context);

        // Act
        await ExecuteWithinPatienceAsync(execution, context);

        // Assert
        UnsavedOutcome.Read(rescheduled.Should().ContainSingle().Subject.JobDataMap).Outcome.Should().Be(carried);
    }

    [Fact]
    public async Task Execute_CarriedOutcomeWriteMarkedTransient_RefiresKeepingTheRejectionCount()
    {
        // Arrange
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("The write failed.", new TransientDbException()));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var carried = Carried(AttemptRowWrite.Completed).Rejected();
        var context = FiringCarrying(JobTriggerFiringOf(JobId), carried);
        var rescheduled = CaptureRescheduledTriggers(context);

        // Act
        await ExecuteWithinPatienceAsync(execution, context);

        // Assert — a failure the driver calls transient is an outage: the row is not read to tell, and the rejection
        // counted before is carried as it was.
        UnsavedOutcome.Read(rescheduled.Should().ContainSingle().Subject.JobDataMap).Outcome.Should().Be(carried);
        await repository.DidNotReceiveWithAnyArgs().ReadAttemptAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Execute_CarriedOutcomeRejectedAndRefireRefused_DeadLettersTheCarriedAttempt()
    {
        // Arrange — neither the outcome nor the trigger carrying it again will ever be taken.
        var repository = RejectingCompletions();
        repository.TryDeadLetterAsync(Arg.Any<UnfinishedRow>(), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), Carried(AttemptRowWrite.Completed));
        RejectEveryTrigger(context.Scheduler);

        // Act
        await ExecuteWithinPatienceAsync(execution, context);

        // Assert — the firing that met the refusals tries the outcome until its rejections are spent, then dead-letters.
        await repository.Received(OneFiringOfWrites * UnsavedOutcome.MaxRejections)
            .TryCompleteAsync(Arg.Any<AttemptRef>(), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>());
        await repository.Received(1).TryDeadLetterAsync(
            new UnfinishedRow(JobId, JobStatus.Processing, MaxAttempts), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>());
        provider.GetRequiredService<IJobMetrics>().Received(1).Record(JobLifecycleEvent.DeadLettered, ProbeJobType);
    }

    [Fact]
    public async Task Execute_SpentOutcomeAndRefireToReclaimRefused_DeadLettersTheCarriedAttempt()
    {
        // Arrange — the outcome is refused its last allowed time, and the re-fire to re-claim the row is refused too.
        var repository = RejectingCompletions();
        repository.TryDeadLetterAsync(Arg.Any<UnfinishedRow>(), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var carried = Carried(AttemptRowWrite.Completed) with { Rejections = UnsavedOutcome.MaxRejections - 1 };
        var context = FiringCarrying(JobTriggerFiringOf(JobId), carried);
        RejectEveryTrigger(context.Scheduler);

        // Act
        await ExecuteWithinPatienceAsync(execution, context);

        // Assert — the spent outcome is not written again; the row is dead-lettered only at the attempt it came from.
        await repository.Received(OneFiringOfWrites)
            .TryCompleteAsync(Arg.Any<AttemptRef>(), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>());
        await repository.Received(1).TryDeadLetterAsync(
            new UnfinishedRow(JobId, JobStatus.Processing, MaxAttempts), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_UnknownJobTypeCarryingRejectedOutcomeAndRefireRefused_DeadLettersTheCarriedAttempt()
    {
        // Arrange — a node without the handler writes the outcome because the scheduler keeps refusing its trigger,
        // and the database refuses the outcome its last allowed time.
        var repository = RejectingCompletions();
        repository.TryDeadLetterAsync(Arg.Any<UnfinishedRow>(), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var carried = Carried(AttemptRowWrite.Completed) with { Rejections = UnsavedOutcome.MaxRejections - 1 };
        var context = FiringCarrying(FiringOf(JobId, jobType: "Orphan"), carried);
        RejectEveryTrigger(context.Scheduler);

        // Act
        await ExecuteWithinPatienceAsync(execution, context);

        // Assert
        await repository.Received(1).TryDeadLetterAsync(
            new UnfinishedRow(JobId, JobStatus.Processing, MaxAttempts), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().TryClaimAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Execute_CarriedOutcomeRejectedWhileRefireRefused_StoresTheTriggerWithEveryRejectionCounted()
    {
        // Arrange — the write breaks a rule of the table; the scheduler refuses the re-fire until the firing has tried
        // the outcome itself once more, and then stores it.
        var repository = RejectingCompletions();
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var carried = Carried(AttemptRowWrite.Completed);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), carried);
        var stored = StoreTriggersAfterRefusing(context.Scheduler, RefusedRefire.MaxRefusals);

        // Act
        await ExecuteWithinPatienceAsync(execution, context);

        // Assert — one rejection of the firing's own write, and one of the write it made while the scheduler refused.
        UnsavedOutcome.Read(stored.Should().ContainSingle().Subject.JobDataMap).Outcome.Should()
            .Be(carried with { Rejections = 2 });
    }

    [Fact]
    public async Task Execute_UnknownJobTypeCarryingOutcomeRejectedWhileRefused_StoresTheTriggerWithTheRejectionCounted()
    {
        // Arrange — a node without the handler writes the outcome once the scheduler has refused its trigger enough
        // times, the database refuses that write, and then the scheduler stores the trigger.
        var repository = RejectingCompletions();
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var carried = Carried(AttemptRowWrite.Completed);
        var context = FiringCarrying(FiringOf(JobId, jobType: "Orphan"), carried);
        var stored = StoreTriggersAfterRefusing(context.Scheduler, RefusedRefire.MaxRefusals);

        // Act
        await ExecuteWithinPatienceAsync(execution, context);

        // Assert
        var trigger = stored.Should().ContainSingle().Subject;
        UnsavedOutcome.Read(trigger.JobDataMap).Outcome.Should().Be(carried with { Rejections = 1 });
        trigger.JobDataMap.GetString(BackgroundJobExecution.ReclaimKey).Should().Be(bool.TrueString);
    }

    [Fact]
    public async Task RecordUnsavedAsync_WriteFailsWhileTheHostStops_LeavesItUnwritableWithoutReadingTheRow()
    {
        // Arrange — a stopping host counts as an outage, so no rejection is counted against the outcome.
        var repository = RejectingCompletions();
        await using var provider = Services(repository, new ObservedRun());
        var recorder = ActivatorUtilities.CreateInstance<JobOutcomeRecorder>(provider);
        provider.GetRequiredService<JobsShutdownSignal>().Raise();

        // Act
        var written = await recorder.RecordUnsavedAsync(new ReclaimableJob(JobId, ProbeJobType), Carried(AttemptRowWrite.Completed));

        // Assert
        written.Should().Be(OutcomeWrite.Unwritable);
        await repository.DidNotReceiveWithAnyArgs().ReadAttemptAsync(default, TestContext.Current.CancellationToken);
    }

    // A completion write that throws on every try, as one breaking a constraint does, while the row still reads.
    private static IBackgroundJobStateRepository RejectingCompletions()
    {
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("The row breaks a constraint."));
        repository.ReadAttemptAsync(JobId, Arg.Any<CancellationToken>())
            .Returns(new JobAttemptState(JobStatus.Processing, MaxAttempts));
        return repository;
    }

    // The scheduler refuses the firing's own trigger for a reason of its own the first times, and then stores it.
    private static List<ITrigger> StoreTriggersAfterRefusing(IScheduler scheduler, int refusals)
    {
        var stored = new List<ITrigger>();
        var tries = 0;
        scheduler.Exists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(true);
        scheduler
            .RescheduleJob(Arg.Any<TriggerKey>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<DateTimeOffset?>>(call =>
            {
                if (++tries <= refusals)
                {
                    throw new SchedulerException("The trigger could not be stored.");
                }

                stored.Add(call.ArgAt<ITrigger>(1));
                return ValueTask.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow);
            });
        return stored;
    }

    private static Task ExecuteWithinPatienceAsync(BackgroundJobExecution execution, IJobExecutionContext context) =>
        execution.Execute(context, TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(WritePatience, TestContext.Current.CancellationToken);

    private sealed class TransientDbException : DbException
    {
        public override bool IsTransient => true;
    }
}
