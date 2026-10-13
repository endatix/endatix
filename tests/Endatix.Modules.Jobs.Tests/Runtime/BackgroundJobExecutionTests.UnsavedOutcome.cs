using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.Core;
using Quartz;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// An attempt whose handler decided its outcome, which could not be written: the re-fire carries the outcome and
/// writes it, fenced on that attempt, instead of claiming the row for another attempt.
/// </summary>
public sealed partial class BackgroundJobExecutionTests
{
    private const string HandlerFailure = "Form 12 has no schema.";

    private static readonly DateTime CarriedClaimedAt = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime CarriedEndedAt = CarriedClaimedAt.AddSeconds(4);

    // Long enough for a firing's outcome write to fail on every try, with its waits between them.
    private static readonly TimeSpan WritePatience = TimeSpan.FromSeconds(30);

    // Every try one firing makes at an outcome write.
    private static readonly int OneFiringOfWrites = JobOutcomeRecorder.WriteRetryDelays.Length + 1;

    [Fact]
    public async Task Execute_LastAttemptCompletionKeepsFailing_RefiresCarryingTheCompletion()
    {
        // Arrange
        var repository = ClaimingRepository(attempt: MaxAttempts);
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);
        var rescheduled = CaptureRescheduledTriggers(context);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert — still marked to re-claim, so a node that cannot read the outcome takes the job over as before.
        var refire = rescheduled.Should().ContainSingle().Subject;
        refire.JobDataMap.GetString(BackgroundJobExecution.ReclaimKey).Should().Be(bool.TrueString);
        UnsavedOutcome.Read(refire.JobDataMap).Outcome.Should()
            .BeEquivalentTo(new { Attempt = MaxAttempts, Write = AttemptRowWrite.Completed, ErrorMessage = (string?)null });
        provider.GetRequiredService<IJobMetrics>().DidNotReceive().Record(JobLifecycleEvent.Completed, Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_ReturnedFailureCannotBeWritten_RefiresCarryingTheFailureMessage()
    {
        // Arrange
        var repository = ClaimingRepository(attempt: 1);
        repository.TryFailAsync(new AttemptRef(JobId, 1), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun { FailWith = HandlerFailure });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);
        var rescheduled = CaptureRescheduledTriggers(context);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        UnsavedOutcome.Read(rescheduled.Should().ContainSingle().Subject.JobDataMap).Outcome.Should()
            .BeEquivalentTo(new { Attempt = 1, Write = AttemptRowWrite.Failed, ErrorMessage = HandlerFailure });
    }

    [Fact]
    public async Task Execute_ThrowWithAttemptsLeftCannotBeRecorded_RefiresToReclaimWithoutCarriedOutcome()
    {
        // Arrange — the retry's trigger is stored, then the Retrying write keeps failing.
        var repository = ClaimingRepository(attempt: 1);
        repository.RecordFailedAttemptAsync(new AttemptRef(JobId, 1), Arg.Any<RetryableFailure>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun { Throw = true });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);
        var rescheduled = CaptureRescheduledTriggers(context);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert — the throw is judged again by the re-claim, against the budget as it is then.
        rescheduled.Should().HaveCount(2);
        rescheduled[1].JobDataMap.GetString(BackgroundJobExecution.ReclaimKey).Should().Be(bool.TrueString);
        rescheduled[1].JobDataMap.ContainsKey(UnsavedOutcome.DataKey).Should().BeFalse();
    }

    [Fact]
    public async Task Execute_FiringCarriesCompletion_CompletesTheCarriedAttemptWithoutClaimingOrRunning()
    {
        // Arrange
        var observed = new ObservedRun();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), Carried(AttemptRowWrite.Completed));

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert — the row records when the attempt ended, and the attempt's end is counted once, here.
        await repository.Received(1).TryCompleteAsync(
            new AttemptRef(JobId, MaxAttempts), Arg.Is<JobFinish>(finish => finish.UtcNow == CarriedEndedAt), CancellationToken.None);
        await repository.DidNotReceiveWithAnyArgs().TryClaimAsync(default!, TestContext.Current.CancellationToken);
        await repository.DidNotReceiveWithAnyArgs().TryDeadLetterSpentAsync(default, default, TestContext.Current.CancellationToken);
        observed.ContextTenantId.Should().Be(-1);
        var metrics = provider.GetRequiredService<IJobMetrics>();
        metrics.Received(1).Record(JobLifecycleEvent.Completed, ProbeJobType);
        metrics.DidNotReceive().Record(JobLifecycleEvent.Claimed, Arg.Any<string>());
        metrics.Received(1).ObserveDuration(ProbeJobType, Arg.Any<TimeSpan>(), JobAttemptOutcome.Completed);
        context.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_FiringCarriesFailure_FailsTheCarriedAttemptWithItsMessage()
    {
        // Arrange
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryFailAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), Carried(AttemptRowWrite.Failed, HandlerFailure));

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        await repository.Received(1).TryFailAsync(
            new AttemptRef(JobId, MaxAttempts),
            Arg.Is<AttemptFailure>(failure => failure.ErrorMessage == HandlerFailure && failure.UtcNow == CarriedEndedAt),
            CancellationToken.None);
        provider.GetRequiredService<IJobMetrics>().Received(1).Record(JobLifecycleEvent.Failed, ProbeJobType);
    }

    [Fact]
    public async Task Execute_RecoveredFiringCarriesCompletion_CompletesInsteadOfDeadLettering()
    {
        // Arrange — the node firing the carried outcome stopped, and another node recovers that firing.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringCarrying(RecoveryOf(JobId), Carried(AttemptRowWrite.Completed));

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        await repository.Received(1).TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), CancellationToken.None);
        await repository.DidNotReceiveWithAnyArgs().TryDeadLetterSpentAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Execute_CarriedOutcomeRowMovedOn_LeavesTheRowAndStoresNoTrigger()
    {
        // Arrange — the row was cancelled, taken over or recorded since the attempt ended.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns(false);
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), Carried(AttemptRowWrite.Completed));

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        await repository.Received(1).TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), CancellationToken.None);
        await repository.DidNotReceiveWithAnyArgs().TryClaimAsync(default!, TestContext.Current.CancellationToken);
        provider.GetRequiredService<IJobMetrics>().DidNotReceive().Record(JobLifecycleEvent.Completed, Arg.Any<string>());
        context.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_CarriedOutcomeStillUnwritable_RefiresCarryingItAgain()
    {
        // Arrange
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var carried = Carried(AttemptRowWrite.Completed);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), carried);
        var rescheduled = CaptureRescheduledTriggers(context);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        await repository.Received(JobOutcomeRecorder.WriteRetryDelays.Length + 1)
            .TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), CancellationToken.None);
        UnsavedOutcome.Read(rescheduled.Should().ContainSingle().Subject.JobDataMap).Outcome.Should().Be(carried);
        provider.GetRequiredService<IJobMetrics>().DidNotReceive().Record(JobLifecycleEvent.Completed, Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_FiringCarriesCompletion_ObservesDurationFromClaimToTheAttemptsEnd()
    {
        // Arrange — the re-fire runs an hour after the attempt ended, on a node whose clock says so.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, new ObservedRun());
        provider.GetRequiredService<IDateTimeProvider>().UtcNow.Returns(new DateTimeOffset(CarriedEndedAt.AddHours(1)));
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), Carried(AttemptRowWrite.Completed));

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        provider.GetRequiredService<IJobMetrics>().Received(1)
            .ObserveDuration(ProbeJobType, CarriedEndedAt - CarriedClaimedAt, JobAttemptOutcome.Completed);
    }

    [Fact]
    public async Task Execute_CompletionUnwritableAndRefireRejected_WritesTheCompletionInsteadOfDeadLettering()
    {
        // Arrange — the last attempt succeeds, its write fails on every try, and the scheduler refuses the re-fire.
        var repository = ClaimingRepository(attempt: MaxAttempts);
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns(LandsAfterFailing(OneFiringOfWrites));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);
        RejectEveryTrigger(context.Scheduler);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(WritePatience, TestContext.Current.CancellationToken);

        // Assert — the firing wrote the completion itself, and the attempt's end was counted once.
        ScheduleJobCalls(context.Scheduler).Should().Be(RefusedRefire.MaxRefusals);
        await repository.Received(OneFiringOfWrites + 1)
            .TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), CancellationToken.None);
        await repository.DidNotReceiveWithAnyArgs().TryDeadLetterAsync(default, default, TestContext.Current.CancellationToken);
        var metrics = provider.GetRequiredService<IJobMetrics>();
        metrics.Received(1).Record(JobLifecycleEvent.Completed, ProbeJobType);
        metrics.DidNotReceive().Record(JobLifecycleEvent.DeadLettered, Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_FiringCarriesFailureAndRefireRejected_WritesTheFailureInsteadOfDeadLettering()
    {
        // Arrange — the carried failure cannot be written at first, and the scheduler refuses carrying it again.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryFailAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns(LandsAfterFailing(OneFiringOfWrites));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), Carried(AttemptRowWrite.Failed, HandlerFailure));
        RejectEveryTrigger(context.Scheduler);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(WritePatience, TestContext.Current.CancellationToken);

        // Assert
        await repository.Received(OneFiringOfWrites + 1).TryFailAsync(
            new AttemptRef(JobId, MaxAttempts),
            Arg.Is<AttemptFailure>(failure => failure.ErrorMessage == HandlerFailure),
            CancellationToken.None);
        await repository.DidNotReceiveWithAnyArgs().TryDeadLetterAsync(default, default, TestContext.Current.CancellationToken);
        provider.GetRequiredService<IJobMetrics>().Received(1).Record(JobLifecycleEvent.Failed, ProbeJobType);
    }

    [Fact]
    public async Task Execute_CarriedOutcomeOfRejectedRefireStillUnwritable_HoldsFiringWithoutDeadLettering()
    {
        // Arrange — every write fails; the write after the refusals tells the test the firing got that far.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        var writeAfterRefusals = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tries = 0;
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ =>
            {
                if (++tries > OneFiringOfWrites)
                {
                    writeAfterRefusals.TrySetResult();
                }

                throw new TimeoutException("The database did not answer.");
            });
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringCarrying(JobTriggerFiringOf(JobId), Carried(AttemptRowWrite.Completed));
        RejectEveryTrigger(context.Scheduler);

        // Act
        var executing = execution.Execute(context, TestContext.Current.CancellationToken).AsTask();
        await writeAfterRefusals.Task.WaitAsync(WritePatience, TestContext.Current.CancellationToken);
        var openUntilSignal = !executing.IsCompleted;
        provider.GetRequiredService<JobsShutdownSignal>().Raise();
        await executing.WaitAsync(WritePatience, TestContext.Current.CancellationToken);

        // Assert
        openUntilSignal.Should().BeTrue();
        await repository.DidNotReceiveWithAnyArgs().TryDeadLetterAsync(default, default, TestContext.Current.CancellationToken);
        provider.GetRequiredService<IJobMetrics>().DidNotReceiveWithAnyArgs().Record(default, default!);
    }

    [Fact]
    public async Task Execute_UnknownJobTypeCarryingOutcome_KeepsTheOutcomeOnTheRescheduledTrigger()
    {
        // Arrange
        var carried = Carried(AttemptRowWrite.Completed);
        var context = FiringCarrying(FiringOf(JobId, jobType: "Orphan"), carried);

        // Act
        var rescheduled = await DeclinedTriggerOfAsync(context);

        // Assert
        UnsavedOutcome.Read(rescheduled.JobDataMap).Outcome.Should().Be(carried);
    }

    // A write that throws, as it does while the database is away, for its first tries, and then lands.
    private static Func<CallInfo, Task<bool>> LandsAfterFailing(int failures)
    {
        var tries = 0;
        return _ => ++tries > failures ? Task.FromResult(true) : throw new TimeoutException("The database did not answer.");
    }

    private static UnsavedOutcome Carried(AttemptRowWrite write, string? errorMessage = null) =>
        new(MaxAttempts, write, errorMessage, CarriedClaimedAt, CarriedEndedAt);

    // A re-fire as the job wrapper stores it for an outcome that could not be written.
    private static IJobExecutionContext FiringCarrying(IJobExecutionContext context, UnsavedOutcome carried)
    {
        context.MergedJobDataMap.Returns(new JobDataMap
        {
            [BackgroundJobExecution.JobIdKey] = JobId.ToString(),
            [BackgroundJobExecution.ReclaimKey] = bool.TrueString,
            [UnsavedOutcome.DataKey] = carried.Serialize(),
        });
        return context;
    }

    private static List<ITrigger> CaptureRescheduledTriggers(IJobExecutionContext context)
    {
        var rescheduled = new List<ITrigger>();
        context.Scheduler
            .RescheduleJob(context.Trigger.Key, Arg.Do<ITrigger>(rescheduled.Add), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.UtcNow);
        return rescheduled;
    }
}
