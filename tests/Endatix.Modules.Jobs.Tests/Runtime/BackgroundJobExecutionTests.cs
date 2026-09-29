using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class BackgroundJobExecutionTests
{
    public static TheoryData<string, string, int, int, string, bool> Outcomes => new()
    {
        { "success", nameof(AttemptEnd.Succeeded), 1, 3, nameof(AttemptRowWrite.Completed), false },
        { "failure result", nameof(AttemptEnd.ReturnedFailure), 1, 3, nameof(AttemptRowWrite.Failed), false },
        { "throw with attempts left", nameof(AttemptEnd.Threw), 2, 3, nameof(AttemptRowWrite.Retrying), true },
        { "throw on the last attempt", nameof(AttemptEnd.Threw), 3, 3, nameof(AttemptRowWrite.DeadLettered), false },
        { "throw past the budget after a recovery", nameof(AttemptEnd.Threw), 4, 3, nameof(AttemptRowWrite.DeadLettered), false },
        { "row canceled", nameof(AttemptEnd.Canceled), 1, 3, nameof(AttemptRowWrite.None), false },
        { "host shutdown", nameof(AttemptEnd.HostShutdown), 1, 3, nameof(AttemptRowWrite.None), false },
        { "attempt taken over", nameof(AttemptEnd.Superseded), 1, 3, nameof(AttemptRowWrite.None), false },
    };

    [Theory]
    [MemberData(nameof(Outcomes))]
    public void DecideOutcome_EachHandlerOutcome_ReturnsExpectedRowAndQuartzAction(
        string caseId,
        string endName,
        int attemptCount,
        int maxAttempts,
        string expectedRowName,
        bool expectedRethrow)
    {
        // Arrange — the case id names the row of the outcome table under test.
        _ = caseId;
        var end = Enum.Parse<AttemptEnd>(endName);
        var expectedRow = Enum.Parse<AttemptRowWrite>(expectedRowName);

        // Act
        var decision = AttemptDecision.Decide(end, attemptCount, maxAttempts);

        // Assert
        decision.Should().Be(new AttemptDecision(expectedRow, expectedRethrow));
    }

    [Fact]
    public async Task ResolveContext_ClaimedRow_UsesRowTenantAndLeavesAmbientUnset()
    {
        // Arrange — the host serves no tenant, and the claimed row belongs to tenant A.
        var observed = new ObservedRun();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(ClaimOfJob(recovering: false), Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, ProbeJobType, TenantA, "{}", 1, null, JobStatus.Processing));
        repository.TryCompleteAsync(new AttemptRef(JobId, 1), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(FiringOf(JobId), TestContext.Current.CancellationToken);

        // Assert — the handler scopes its queries by the context's tenant, and nothing made tenant A ambient.
        observed.ContextTenantId.Should().Be(TenantA);
        observed.AmbientTenantId.Should().Be(0);
        await repository.Received(1).TryCompleteAsync(new AttemptRef(JobId, 1), Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task Execute_RecoveredOnLastAttempt_DeadLettersWithoutRunningHandler()
    {
        // Arrange — the node running the job's last attempt stopped, and another node recovers it.
        var observed = new ObservedRun();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryDeadLetterSpentAsync(
                new AttemptRef(JobId, 3),
                Arg.Is<AttemptFailure>(failure => failure.ErrorMessage == BackgroundJobMessages.StoppedOnLastAttempt),
                Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(RecoveryOf(JobId), TestContext.Current.CancellationToken);

        // Assert
        observed.ContextTenantId.Should().Be(-1);
        await repository.DidNotReceiveWithAnyArgs().TryClaimAsync(default!, default);
    }

    [Fact]
    public async Task Execute_RecoveredWithAttemptsLeft_ReclaimsAndRuns()
    {
        // Arrange
        var observed = new ObservedRun();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryDeadLetterSpentAsync(
                Arg.Is<AttemptRef>(attempt => attempt.JobId == JobId), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns(false);
        repository
            .TryClaimAsync(ClaimOfJob(recovering: true), Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, ProbeJobType, TenantA, "{}", 2, null, JobStatus.Processing));
        repository.TryCompleteAsync(new AttemptRef(JobId, 2), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(RecoveryOf(JobId), TestContext.Current.CancellationToken);

        // Assert
        observed.ContextTenantId.Should().Be(TenantA);
        await repository.Received(1).TryCompleteAsync(new AttemptRef(JobId, 2), Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task Execute_HandlerSucceedsAsShutdownStarts_RecordsCompletion()
    {
        // Arrange — the host stops waiting just as the handler finishes its work.
        var observed = new ObservedRun { RaiseShutdown = true };
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(ClaimOfJob(recovering: false), Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, ProbeJobType, TenantA, "{}", 1, null, JobStatus.Processing));
        repository.TryCompleteAsync(new AttemptRef(JobId, 1), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(FiringOf(JobId), TestContext.Current.CancellationToken);

        // Assert — recorded, so recovery does not run the finished work again.
        provider.GetRequiredService<JobsShutdownSignal>().IsRaised.Should().BeTrue();
        await repository.Received(1).TryCompleteAsync(new AttemptRef(JobId, 1), Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task Execute_HandlerStoppedByShutdown_RecordsAbandonedAndWritesNothing()
    {
        // Arrange
        var observed = new ObservedRun { RaiseShutdown = true, ThrowAfterShutdown = true };
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(ClaimOfJob(recovering: false), Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, ProbeJobType, TenantA, "{}", 1, null, JobStatus.Processing));
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(FiringOf(JobId), TestContext.Current.CancellationToken);

        // Assert
        var metrics = provider.GetRequiredService<IJobMetrics>();
        metrics.Received(1).Record(JobLifecycleEvent.Abandoned, ProbeJobType);
        metrics.Received(1).ObserveDuration(ProbeJobType, Arg.Any<TimeSpan>(), JobAttemptOutcome.Abandoned);
        await repository.DidNotReceiveWithAnyArgs().RecordFailedAttemptAsync(default, default, default);
    }

    [Fact]
    public async Task Execute_RecoveredRunThrowsWithAttemptsLeft_SchedulesOwnTriggerBeforeRecordingRetry()
    {
        // Arrange — a recovery firing has no retry policy, so the next attempt needs a trigger of its own.
        var observed = new ObservedRun { Throw = true };
        var steps = new List<string>();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(ClaimOfJob(recovering: true), Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, ProbeJobType, TenantA, "{}", 2, null, JobStatus.Processing));
        repository
            .RecordFailedAttemptAsync(
                new AttemptRef(JobId, 2),
                Arg.Is<RetryableFailure>(failure => failure.MaxAttempts == 3),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                steps.Add("record retrying");
                return true;
            });
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = RecoveryOf(JobId);
        ITrigger? scheduled = null;
        context.Scheduler
            .ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                scheduled = call.Arg<ITrigger>();
                steps.Add("schedule trigger");
                return new ValueTask<DateTimeOffset>(DateTimeOffset.UtcNow);
            });

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        steps.Should().Equal("schedule trigger", "record retrying");
        scheduled!.Key.Should().Be(new TriggerKey(JobId.ToString(), ProbeJobType));
        scheduled.RetryPolicy.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_OutcomeWriteFailsOnce_TriesAgainAndRecordsCompletion()
    {
        // Arrange
        var repository = ClaimingRepository(attempt: 1);
        repository.TryCompleteAsync(new AttemptRef(JobId, 1), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new TimeoutException("The database did not answer."), _ => Task.FromResult(true));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        await repository.Received(2).TryCompleteAsync(new AttemptRef(JobId, 1), Arg.Any<DateTime>(), CancellationToken.None);
        provider.GetRequiredService<IJobMetrics>().Received(1).Record(JobLifecycleEvent.Completed, ProbeJobType);
        context.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_OutcomeWriteKeepsFailing_ReschedulesItsTriggerToReclaimTheRow()
    {
        // Arrange
        var repository = ClaimingRepository(attempt: 1);
        repository.TryCompleteAsync(new AttemptRef(JobId, 1), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);
        ITrigger? rescheduled = null;
        context.Scheduler
            .RescheduleJob(context.Trigger.Key, Arg.Do<ITrigger>(trigger => rescheduled = trigger), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.UtcNow);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert — the firing trigger itself fires again, marked to take over the row this attempt left Processing.
        await repository.Received(JobOutcomeRecorder.WriteRetryDelays.Length + 1)
            .TryCompleteAsync(new AttemptRef(JobId, 1), Arg.Any<DateTime>(), CancellationToken.None);
        rescheduled.Should().NotBeNull();
        rescheduled.Key.Should().Be(context.Trigger.Key);
        rescheduled.JobDataMap.GetString(BackgroundJobExecution.ReclaimKey).Should().Be(bool.TrueString);
        provider.GetRequiredService<IJobMetrics>().DidNotReceive().Record(JobLifecycleEvent.Completed, Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_OutcomeWriteFailsDuringShutdown_LeavesTheFiringForRecovery()
    {
        // Arrange — the host stops as the handler succeeds, and the write that follows fails.
        var repository = ClaimingRepository(attempt: 1);
        repository.TryCompleteAsync(new AttemptRef(JobId, 1), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun { RaiseShutdown = true });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert — no further tries and no rescheduling on a scheduler that has stopped.
        await repository.Received(1).TryCompleteAsync(new AttemptRef(JobId, 1), Arg.Any<DateTime>(), CancellationToken.None);
        context.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_ReclaimingFiring_ReclaimsTheRowLeftProcessing()
    {
        // Arrange
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(ClaimOfJob(recovering: true), Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, ProbeJobType, TenantA, "{}", 2, null, JobStatus.Processing));
        repository.TryCompleteAsync(new AttemptRef(JobId, 2), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);
        context.MergedJobDataMap.Returns(new JobDataMap
        {
            [BackgroundJobExecution.JobIdKey] = JobId.ToString(),
            [BackgroundJobExecution.ReclaimKey] = bool.TrueString,
        });

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        await repository.Received(1)
            .TryClaimAsync(ClaimOfJob(recovering: true), Arg.Any<CancellationToken>());
        await repository.Received(1).TryCompleteAsync(new AttemptRef(JobId, 2), Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task Execute_AttemptTakenOverWhileRunning_StopsHandlerAndRecordsAbandoned()
    {
        // Arrange — while the handler runs, the row moves on to an attempt another node took over.
        var observed = new ObservedRun { WaitForCancellation = true };
        var repository = ClaimingRepository(attempt: 1);
        repository.ReadAttemptAsync(JobId, Arg.Any<CancellationToken>())
            .Returns(new JobAttemptState(JobStatus.Processing, 2));
        await using var provider = Services(repository, observed, new BackgroundJobsOptions { CancellationPollSeconds = 1 });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(JobTriggerFiringOf(JobId), TestContext.Current.CancellationToken);

        // Assert
        observed.HandlerTokenCancelled.Should().BeTrue();
        await repository.DidNotReceiveWithAnyArgs().TryCompleteAsync(default, default, default);
        await repository.DidNotReceiveWithAnyArgs()
            .RecordFailedAttemptAsync(default, default, default);
        var metrics = provider.GetRequiredService<IJobMetrics>();
        metrics.Received(1).Record(JobLifecycleEvent.Abandoned, ProbeJobType);
        metrics.Received(1).ObserveDuration(ProbeJobType, Arg.Any<TimeSpan>(), JobAttemptOutcome.Abandoned);
    }

    [Fact]
    public async Task Execute_UnknownJobType_DeclinesAndReschedules()
    {
        // Arrange — this node has no handler for "Orphan", yet its trigger fired here.
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        await using var provider = Services(repository, new ObservedRun());
        provider.GetRequiredService<IDateTimeProvider>().UtcNow.Returns(now);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringOf(JobId, jobType: "Orphan");
        ITrigger? rescheduled = null;
        context.Scheduler
            .RescheduleJob(Arg.Any<TriggerKey>(), Arg.Do<ITrigger>(trigger => rescheduled = trigger), Arg.Any<CancellationToken>())
            .Returns(now.AddSeconds(30));

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert — the row is never touched, and the same trigger fires again in thirty seconds.
        repository.ReceivedCalls().Should().BeEmpty();
        rescheduled.Should().NotBeNull();
        rescheduled.Key.Should().Be(context.Trigger.Key);
        rescheduled.StartTimeUtc.Should().BeCloseTo(now.AddSeconds(30), TimeSpan.FromSeconds(2));
        rescheduled.JobDataMap.GetString(BackgroundJobExecution.JobIdKey).Should().Be(JobId.ToString());
        rescheduled.ExecutionGroup.Should().Be("Orphan");
    }

    [Fact]
    public async Task Execute_FiringWithoutJobId_TouchesNothing()
    {
        // Arrange — a durable job fired by hand carries no job id.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringOf(JobId);
        context.MergedJobDataMap.Returns(new JobDataMap());

        // Act
        var act = async () => await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        repository.ReceivedCalls().Should().BeEmpty();
        context.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ClaimedRow_CopiesRowFields()
    {
        // Arrange
        var resolver = new JobRowExecutionContextResolver();
        var claimed = new ClaimedJob(JobId, ProbeJobType, TenantA, """{"a":1}""", 3, "trace", JobStatus.Processing);

        // Act
        var context = resolver.Resolve(claimed);

        // Assert
        context.Should().Be(new BackgroundJobContext(JobId, ProbeJobType, TenantA, """{"a":1}""", 3));
    }
}
