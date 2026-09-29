using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class BackgroundJobExecutionTests
{
    private const long TenantA = 101;
    private const long JobId = 42;
    private const string JobType = "TenantProbe";

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
            .TryClaimAsync(JobId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), false, Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, JobType, TenantA, "{}", 1, null, JobStatus.Processing));
        repository.TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(FiringOf(JobId), TestContext.Current.CancellationToken);

        // Assert — the handler scopes its queries by the context's tenant, and nothing made tenant A ambient.
        observed.ContextTenantId.Should().Be(TenantA);
        observed.AmbientTenantId.Should().Be(0);
        await repository.Received(1).TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task Execute_RecoveredOnLastAttempt_DeadLettersWithoutRunningHandler()
    {
        // Arrange — the node running the job's last attempt stopped, and another node recovers it.
        var observed = new ObservedRun();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryDeadLetterSpentAsync(JobId, 3, BackgroundJobMessages.StoppedOnLastAttempt, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(RecoveryOf(JobId), TestContext.Current.CancellationToken);

        // Assert
        observed.ContextTenantId.Should().Be(-1);
        await repository.DidNotReceiveWithAnyArgs().TryClaimAsync(default, default!, default, default, default);
    }

    [Fact]
    public async Task Execute_RecoveredWithAttemptsLeft_ReclaimsAndRuns()
    {
        // Arrange
        var observed = new ObservedRun();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryDeadLetterSpentAsync(JobId, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);
        repository
            .TryClaimAsync(JobId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), true, Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, JobType, TenantA, "{}", 2, null, JobStatus.Processing));
        repository.TryCompleteAsync(JobId, 2, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(RecoveryOf(JobId), TestContext.Current.CancellationToken);

        // Assert
        observed.ContextTenantId.Should().Be(TenantA);
        await repository.Received(1).TryCompleteAsync(JobId, 2, Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task Execute_HandlerSucceedsAsShutdownStarts_RecordsCompletion()
    {
        // Arrange — the host stops waiting just as the handler finishes its work.
        var observed = new ObservedRun { RaiseShutdown = true };
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(JobId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), false, Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, JobType, TenantA, "{}", 1, null, JobStatus.Processing));
        repository.TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(FiringOf(JobId), TestContext.Current.CancellationToken);

        // Assert — recorded, so recovery does not run the finished work again.
        provider.GetRequiredService<JobsShutdownSignal>().IsRaised.Should().BeTrue();
        await repository.Received(1).TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task Execute_HandlerStoppedByShutdown_RecordsAbandonedAndWritesNothing()
    {
        // Arrange
        var observed = new ObservedRun { RaiseShutdown = true, ThrowAfterShutdown = true };
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(JobId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), false, Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, JobType, TenantA, "{}", 1, null, JobStatus.Processing));
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(FiringOf(JobId), TestContext.Current.CancellationToken);

        // Assert
        var metrics = provider.GetRequiredService<IJobMetrics>();
        metrics.Received(1).Record(JobLifecycleEvent.Abandoned, JobType);
        metrics.Received(1).ObserveDuration(JobType, Arg.Any<TimeSpan>(), JobAttemptOutcome.Abandoned);
        await repository.DidNotReceiveWithAnyArgs().RecordFailedAttemptAsync(default, default, default, default, default!, default, default);
    }

    [Fact]
    public async Task Execute_RecoveredRunThrowsWithAttemptsLeft_SchedulesOwnTriggerBeforeRecordingRetry()
    {
        // Arrange — a recovery firing has no retry policy, so the next attempt needs a trigger of its own.
        var observed = new ObservedRun { Throw = true };
        var steps = new List<string>();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(JobId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), true, Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, JobType, TenantA, "{}", 2, null, JobStatus.Processing));
        repository
            .RecordFailedAttemptAsync(JobId, 2, 3, Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
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
        scheduled!.Key.Should().Be(new TriggerKey(JobId.ToString(), JobType));
        scheduled.RetryPolicy.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_OutcomeWriteFailsOnce_TriesAgainAndRecordsCompletion()
    {
        // Arrange
        var repository = ClaimingRepository(attempt: 1);
        repository.TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new TimeoutException("The database did not answer."), _ => Task.FromResult(true));
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        await repository.Received(2).TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), CancellationToken.None);
        provider.GetRequiredService<IJobMetrics>().Received(1).Record(JobLifecycleEvent.Completed, JobType);
        context.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_OutcomeWriteKeepsFailing_ReschedulesItsTriggerToReclaimTheRow()
    {
        // Arrange
        var repository = ClaimingRepository(attempt: 1);
        repository.TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
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
        await repository.Received(BackgroundJobExecution.OutcomeWriteRetryDelays.Length + 1)
            .TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), CancellationToken.None);
        rescheduled.Should().NotBeNull();
        rescheduled!.Key.Should().Be(context.Trigger.Key);
        rescheduled.JobDataMap.GetString(BackgroundJobExecution.ReclaimKey).Should().Be(bool.TrueString);
        provider.GetRequiredService<IJobMetrics>().DidNotReceive().Record(JobLifecycleEvent.Completed, Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_OutcomeWriteFailsDuringShutdown_LeavesTheFiringForRecovery()
    {
        // Arrange — the host stops as the handler succeeds, and the write that follows fails.
        var repository = ClaimingRepository(attempt: 1);
        repository.TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TimeoutException("The database did not answer."));
        await using var provider = Services(repository, new ObservedRun { RaiseShutdown = true });
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = JobTriggerFiringOf(JobId);

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert — no further tries and no rescheduling on a scheduler that has stopped.
        await repository.Received(1).TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), CancellationToken.None);
        context.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_ReclaimingFiring_ReclaimsTheRowLeftProcessing()
    {
        // Arrange
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(JobId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), true, Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, JobType, TenantA, "{}", 2, null, JobStatus.Processing));
        repository.TryCompleteAsync(JobId, 2, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
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
            .TryClaimAsync(JobId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), true, Arg.Any<CancellationToken>());
        await repository.Received(1).TryCompleteAsync(JobId, 2, Arg.Any<DateTime>(), CancellationToken.None);
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
        await repository.DidNotReceiveWithAnyArgs().TryCompleteAsync(default, default, default, default);
        await repository.DidNotReceiveWithAnyArgs()
            .RecordFailedAttemptAsync(default, default, default, default, default!, default, default);
        var metrics = provider.GetRequiredService<IJobMetrics>();
        metrics.Received(1).Record(JobLifecycleEvent.Abandoned, JobType);
        metrics.Received(1).ObserveDuration(JobType, Arg.Any<TimeSpan>(), JobAttemptOutcome.Abandoned);
    }

    [Fact]
    public void Resolve_ClaimedRow_CopiesRowFields()
    {
        // Arrange
        var resolver = new JobRowExecutionContextResolver();
        var claimed = new ClaimedJob(JobId, JobType, TenantA, """{"a":1}""", 3, "trace", JobStatus.Processing);

        // Act
        var context = resolver.Resolve(claimed);

        // Assert
        context.Should().Be(new BackgroundJobContext(JobId, JobType, TenantA, """{"a":1}""", 3));
    }

    private static ServiceProvider Services(
        IBackgroundJobStateRepository repository,
        ObservedRun observed,
        BackgroundJobsOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repository);
        services.AddScoped<ITenantContext>(_ => new FixedTenantContext(0));
        services.AddSingleton(observed);
        services.AddScoped<IBackgroundJobHandler, TenantObservingHandler>();
        services.AddSingleton(provider => JobHandlerRegistry.Build(provider));
        services.AddSingleton<IJobExecutionContextResolver, JobRowExecutionContextResolver>();
        services.AddSingleton(Substitute.For<IDateTimeProvider>());
        services.AddSingleton(Options.Create(options ?? new BackgroundJobsOptions()));
        services.AddSingleton<JobsShutdownSignal>();
        services.AddSingleton(Substitute.For<IJobMetrics>());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        return services.BuildServiceProvider();
    }

    private static IJobExecutionContext FiringOf(long jobId)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(new JobDataMap { [BackgroundJobExecution.JobIdKey] = jobId.ToString() });
        context.Recovering.Returns(false);
        return context;
    }

    private static IBackgroundJobStateRepository ClaimingRepository(int attempt)
    {
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(JobId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), false, Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, JobType, TenantA, "{}", attempt, null, JobStatus.Processing));
        return repository;
    }

    // A firing of the job's own trigger, as enqueueing schedules it.
    private static IJobExecutionContext JobTriggerFiringOf(long jobId)
    {
        var context = FiringOf(jobId);
        context.JobDetail.Returns(QuartzRegistration.DurableJobFor(JobType));
        context.Trigger.Returns(QuartzRegistration.TriggerFor(jobId, JobType, new BackgroundJobsOptions().ResolvePolicy(JobType)));
        context.Scheduler.Returns(Substitute.For<IScheduler>());
        return context;
    }

    private static IJobExecutionContext RecoveryOf(long jobId)
    {
        var context = FiringOf(jobId);
        context.Recovering.Returns(true);
        context.JobDetail.Returns(QuartzRegistration.DurableJobFor(JobType));

        // Shaped like the trigger Quartz creates to recover a dead node's firing: its own key and no retry policy.
        context.Trigger.Returns(TriggerBuilder.Create()
            .WithIdentity("recover_node-a_1", SchedulerConstants.DefaultRecoveryGroup)
            .ForJob(QuartzRegistration.JobKeyFor(JobType))
            .UsingJobData(BackgroundJobExecution.JobIdKey, jobId.ToString())
            .StartNow()
            .Build());
        context.Scheduler.Returns(Substitute.For<IScheduler>());
        return context;
    }

    private sealed class ObservedRun
    {
        public long ContextTenantId { get; set; } = -1;

        public long AmbientTenantId { get; set; } = -1;

        public bool RaiseShutdown { get; init; }

        public bool ThrowAfterShutdown { get; init; }

        public bool Throw { get; init; }

        public bool WaitForCancellation { get; init; }

        public bool HandlerTokenCancelled { get; set; }
    }

    private sealed class TenantObservingHandler(ObservedRun observed, ITenantContext ambient, JobsShutdownSignal shutdown)
        : IBackgroundJobHandler
    {
        public string JobType => BackgroundJobExecutionTests.JobType;

        public async Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken)
        {
            observed.ContextTenantId = job.TenantId;
            observed.AmbientTenantId = ambient.TenantId;
            if (observed.RaiseShutdown)
            {
                shutdown.Raise();
            }

            if (observed.Throw)
            {
                throw new InvalidOperationException("Transient failure.");
            }

            if (observed.ThrowAfterShutdown)
            {
                throw new OperationCanceledException(shutdown.Token);
            }

            if (observed.WaitForCancellation)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    observed.HandlerTokenCancelled = true;
                    throw;
                }
            }

            return Result.Success();
        }
    }
}
