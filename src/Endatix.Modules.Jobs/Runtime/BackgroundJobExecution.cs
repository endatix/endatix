using System.Diagnostics;
using System.Globalization;
using Endatix.Core.Abstractions;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The only scheduler job class. One durable scheduler job per job type points at it, and each trigger carries
/// only the id of the job row it fires. It claims the row, runs the handler and records how the attempt ended.
/// </summary>
/// <remarks>
/// <para>
/// Handlers never see this class or any scheduler type: everything scheduler-specific stays here, so replacing
/// the scheduler rewrites this class and not the handlers.
/// </para>
/// <para>
/// Every write after the claim is fenced on the attempt the claim took, so a run that lost the job — to a
/// cancellation, or to a recovery on another node — changes nothing. The claim, the handler and the outcome each
/// run in a scope of their own, because a <c>DbContext</c> is neither thread-safe nor meant to be held for the
/// length of a job.
/// </para>
/// </remarks>
internal sealed class BackgroundJobExecution(
    IServiceScopeFactory scopeFactory,
    JobHandlerRegistry registry,
    IDateTimeProvider dateTimeProvider,
    IOptions<BackgroundJobsOptions> options,
    JobsShutdownSignal shutdownSignal,
    IJobMetrics metrics,
    ILogger<BackgroundJobExecution> logger) : IJob
{
    /// <summary>The trigger data key that holds the job row's id.</summary>
    public const string JobIdKey = "jobId";

    /// <summary>
    /// The trigger data key that marks a firing scheduled to take the job over from an attempt whose outcome could
    /// not be written, so it re-claims the row that attempt left <c>Processing</c>, as a recovery would.
    /// </summary>
    public const string ReclaimKey = "reclaim";

    /// <summary>How long the wrapper waits before each further try at an outcome write that failed.</summary>
    internal static readonly TimeSpan[] OutcomeWriteRetryDelays =
        [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    /// <summary>How long after its outcome could not be written a job runs again.</summary>
    internal static readonly TimeSpan UnrecordedRefireDelay = TimeSpan.FromSeconds(5);

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var jobId = long.Parse(context.MergedJobDataMap.GetString(JobIdKey)!, CultureInfo.InvariantCulture);
        var claimedAt = dateTimeProvider.UtcNow.UtcDateTime;
        var reclaiming = context.Recovering
            || string.Equals(context.MergedJobDataMap.GetString(ReclaimKey), bool.TrueString, StringComparison.OrdinalIgnoreCase);

        // A recovery re-claims the row and counts a new attempt, so without this a job that takes its node down
        // on its last attempt would run again on every recovery, past its budget and never dead-lettered.
        if (reclaiming && await TryDeadLetterSpentAsync(jobId, context.JobDetail.Key.Name, claimedAt, cancellationToken))
        {
            return;
        }

        var claimed = await ClaimAsync(jobId, claimedAt, reclaiming, cancellationToken);
        if (claimed is null)
        {
            // Returning tells the scheduler this firing is done: the job already ran, is running elsewhere, or
            // was cancelled before it started.
            return;
        }

        Record(JobLifecycleEvent.Claimed, claimed.JobType);

        var policy = options.Value.ResolvePolicy(claimed.JobType);
        var run = await RunHandlerAsync(claimed, policy, cancellationToken);
        var decision = AttemptDecision.Decide(run.End, claimed.AttemptCount, policy.MaxAttempts);

        if (run.End is AttemptEnd.Canceled)
        {
            Record(JobLifecycleEvent.Canceled, claimed.JobType);
            ObserveDuration(claimed.JobType, claimedAt, JobAttemptOutcome.Canceled);
            return;
        }

        if (run.End is AttemptEnd.Superseded)
        {
            // Another attempt owns the row, so nothing this one writes could land; its handler was stopped so the
            // two do not run the work side by side.
            logger.LogWarning(
                "Background job {JobId} attempt {Attempt} was taken over by another attempt; its handler was stopped and nothing is recorded",
                claimed.Id,
                claimed.AttemptCount);
            Record(JobLifecycleEvent.Abandoned, claimed.JobType);
            ObserveDuration(claimed.JobType, claimedAt, JobAttemptOutcome.Abandoned);
            return;
        }

        if (run.End is AttemptEnd.HostShutdown)
        {
            // Indistinguishable from a crash at the same instant, so it is treated like one: the attempt taken
            // at the claim stands, nothing is recorded, and the job runs again on the next node to check in.
            logger.LogInformation(
                "Background job {JobId} attempt {Attempt} was left running at shutdown and will run again",
                claimed.Id,
                claimed.AttemptCount);
            Record(JobLifecycleEvent.Abandoned, claimed.JobType);
            ObserveDuration(claimed.JobType, claimedAt, JobAttemptOutcome.Abandoned);
            return;
        }

        var nextAttemptAt = BackgroundJobRetryPolicy.NextAttemptAt(
            claimed.AttemptCount, dateTimeProvider.UtcNow.UtcDateTime, policy);

        // A firing Quartz created to recover a dead node's job carries no retry policy, so a rethrow would end it
        // for good. The next attempt gets a trigger of its own instead, as enqueueing would have made it. It is
        // scheduled before the row says Retrying: should the row write then not land, the trigger finds a row it
        // cannot claim and does nothing, where the other order could leave a Retrying row with no trigger.
        var needsOwnTrigger = decision.Rethrow && context.Trigger.RetryPolicy is null;
        if (needsOwnTrigger)
        {
            await context.Scheduler.ScheduleJob(
                QuartzRegistration.TriggerFor(claimed.Id, claimed.JobType, policy, new DateTimeOffset(nextAttemptAt)),
                ScheduleJobOptions.Replacing,
                CancellationToken.None);
        }

        var written = await RecordOutcomeAsync(claimed, claimedAt, decision.Row, policy, nextAttemptAt, run);
        var recorded = written is OutcomeWrite.Landed;

        if (written is OutcomeWrite.Unwritable)
        {
            await RefireUnrecordedAsync(context, claimed, policy);
            return;
        }

        if (needsOwnTrigger)
        {
            return;
        }

        if (decision.Rethrow && recorded)
        {
            // No inner exception: it was logged when its message was resolved, and the scheduler would log it
            // again.
            throw new JobExecutionException(
                $"Background job {claimed.Id} attempt {claimed.AttemptCount} failed and will be retried.");
        }
    }

    /// <summary>
    /// Keeps a job whose outcome could not be written from being lost: its trigger would otherwise be used up
    /// with the row left <c>Processing</c>. The job fires again shortly and re-claims the row, whose fenced writes
    /// settle it; if the write had in fact landed, the re-claim finds nothing to take.
    /// </summary>
    private async Task RefireUnrecordedAsync(IJobExecutionContext context, ClaimedJob claimed, BackgroundJobTypePolicy policy)
    {
        if (shutdownSignal.IsRaised)
        {
            // The scheduler has already stopped; the firing it abandoned is recovered by the next node to check in.
            return;
        }

        var fireAt = dateTimeProvider.UtcNow.Add(UnrecordedRefireDelay);
        var again = QuartzRegistration.TriggerFor(claimed.Id, claimed.JobType, policy, fireAt, reclaim: true);
        try
        {
            if (context.Trigger.Key.Equals(again.Key))
            {
                // Rescheduling the firing trigger itself keeps it from being deleted when this firing completes.
                await context.Scheduler.RescheduleJob(context.Trigger.Key, again, CancellationToken.None);
            }
            else
            {
                await context.Scheduler.ScheduleJob(again, ScheduleJobOptions.Replacing, CancellationToken.None);
            }

            logger.LogWarning(
                "Background job {JobId} attempt {Attempt} could not record its outcome and runs again at {FireAt:O}",
                claimed.Id,
                claimed.AttemptCount,
                fireAt);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Background job {JobId} attempt {Attempt} could not record its outcome or be scheduled again; it stays Processing until a node recovers it",
                claimed.Id,
                claimed.AttemptCount);
        }
    }

    private async Task<bool> TryDeadLetterSpentAsync(
        long jobId,
        string jobType,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        bool deadLettered;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IBackgroundJobStateRepository>();
            deadLettered = await repository.TryDeadLetterSpentAsync(
                new AttemptRef(jobId, options.Value.ResolvePolicy(jobType).MaxAttempts),
                new AttemptFailure(BackgroundJobMessages.StoppedOnLastAttempt, utcNow),
                cancellationToken);
        }

        if (deadLettered)
        {
            logger.LogWarning(
                "Background job {JobId} of type {JobType} stopped during its last attempt and was dead-lettered",
                jobId,
                jobType);
            Record(JobLifecycleEvent.DeadLettered, jobType);
        }

        return deadLettered;
    }

    private async Task<ClaimedJob?> ClaimAsync(
        long jobId,
        DateTime claimedAt,
        bool recovering,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBackgroundJobStateRepository>();
        return await repository.TryClaimAsync(new JobClaim(jobId, registry.JobTypes, claimedAt, recovering), cancellationToken);
    }

    private async Task<HandlerRun> RunHandlerAsync(
        ClaimedJob claimed,
        BackgroundJobTypePolicy policy,
        CancellationToken schedulerToken)
    {
        using var runtime = new CancellationTokenSource(policy.MaxRuntime);
        await using var watcher = CancellationWatcher.Start(
            scopeFactory,
            claimed.Id,
            claimed.AttemptCount,
            TimeSpan.FromSeconds(options.Value.CancellationPollSeconds),
            logger);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            schedulerToken, shutdownSignal.Token, runtime.Token, watcher.Token);

        using var activity = BackgroundJobsTelemetry.StartExecution(claimed);

        Result? result = null;
        Exception? thrown = null;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var jobContext = scope.ServiceProvider.GetRequiredService<IJobExecutionContextResolver>().Resolve(claimed);
            var handler = registry.Resolve(scope.ServiceProvider, claimed.JobType)
                ?? throw new InvalidOperationException(
                    $"No background job handler is registered for job type '{claimed.JobType}'.");

            result = await handler.ExecuteAsync(jobContext, linked.Token);
            if (!result.IsSuccess)
            {
                // No description: the message the handler wrote goes on the row.
                activity?.SetStatus(ActivityStatusCode.Error);
            }
        }
        catch (Exception exception)
        {
            thrown = exception;
            // The type name only, because a message can carry secrets, which is why none reaches the row either.
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
        }

        // A handler that returned success did its work whenever the shutdown came, so it is recorded: leaving it
        // for recovery would run that work a second time.
        var end = DecideEnd(
            watcher,
            succeeded: thrown is null && result!.IsSuccess,
            shuttingDown: shutdownSignal.IsRaised || schedulerToken.IsCancellationRequested,
            threw: thrown is not null);

        return new HandlerRun(end, result, thrown, runtime.IsCancellationRequested);
    }

    // Checked in this order: a cancelled or superseded row wins over whatever the handler did, and success wins
    // over a shutdown that arrived after the work was done.
    private static AttemptEnd DecideEnd(CancellationWatcher watcher, bool succeeded, bool shuttingDown, bool threw)
    {
        if (watcher.SawCancellation)
        {
            return AttemptEnd.Canceled;
        }

        if (watcher.SawSupersession)
        {
            return AttemptEnd.Superseded;
        }

        if (succeeded)
        {
            return AttemptEnd.Succeeded;
        }

        if (shuttingDown)
        {
            return AttemptEnd.HostShutdown;
        }

        return threw ? AttemptEnd.Threw : AttemptEnd.ReturnedFailure;
    }

    /// <summary>How the handler's run ended, and what it returned or threw.</summary>
    private sealed record HandlerRun(AttemptEnd End, Result? Result, Exception? Thrown, bool RuntimeReached);

    /// <summary>
    /// Makes the one fenced write that ends the attempt, and records its event and duration only when that write
    /// landed: a lost write means another run owns the row, and an event recorded for it would count an attempt
    /// twice. A write that throws is tried again a few times, because a database that blinked would otherwise lose
    /// the outcome of work that is done.
    /// </summary>
    private async Task<OutcomeWrite> RecordOutcomeAsync(
        ClaimedJob claimed,
        DateTime claimedAt,
        AttemptRowWrite write,
        BackgroundJobTypePolicy policy,
        DateTime nextAttemptAt,
        HandlerRun run)
    {
        var endedAt = dateTimeProvider.UtcNow.UtcDateTime;
        var (lifecycleEvent, outcome) = write switch
        {
            AttemptRowWrite.Completed => (JobLifecycleEvent.Completed, JobAttemptOutcome.Completed),
            AttemptRowWrite.Failed => (JobLifecycleEvent.Failed, JobAttemptOutcome.Failed),
            AttemptRowWrite.Retrying => (JobLifecycleEvent.RetryScheduled, JobAttemptOutcome.RetryScheduled),
            _ => (JobLifecycleEvent.DeadLettered, JobAttemptOutcome.DeadLettered),
        };

        // Resolved once, before any try: resolving a thrown message logs the exception.
        var errorMessage = write switch
        {
            AttemptRowWrite.Completed => null,
            AttemptRowWrite.Failed => FailureMessage(run.Result!),
            _ => ThrownMessage(claimed, run.Thrown!, run.RuntimeReached),
        };

        var written = await WriteOutcomeWithRetriesAsync(claimed, write, policy, nextAttemptAt, errorMessage, endedAt, outcome);
        if (written is not OutcomeWrite.Landed)
        {
            return written;
        }

        Record(lifecycleEvent, claimed.JobType);
        ObserveDuration(claimed.JobType, claimedAt, outcome);
        return OutcomeWrite.Landed;
    }

    private async Task<OutcomeWrite> WriteOutcomeWithRetriesAsync(
        ClaimedJob claimed,
        AttemptRowWrite write,
        BackgroundJobTypePolicy policy,
        DateTime nextAttemptAt,
        string? errorMessage,
        DateTime endedAt,
        JobAttemptOutcome outcome)
    {
        // One first try, then one more after each delay.
        for (var attempt = 0; attempt <= OutcomeWriteRetryDelays.Length; attempt++)
        {
            try
            {
                if (await WriteOutcomeAsync(claimed, write, policy, nextAttemptAt, errorMessage, endedAt))
                {
                    return OutcomeWrite.Landed;
                }

                logger.LogWarning(
                    "Background job {JobId} ended attempt {Attempt} as {Outcome}, but the row had moved on and was left as it is",
                    claimed.Id,
                    claimed.AttemptCount,
                    outcome);
                return OutcomeWrite.RowMovedOn;
            }
            catch (Exception exception) when (attempt < OutcomeWriteRetryDelays.Length && !shutdownSignal.IsRaised)
            {
                logger.LogWarning(
                    exception,
                    "Recording {Outcome} for background job {JobId} attempt {Attempt} failed; trying again",
                    outcome,
                    claimed.Id,
                    claimed.AttemptCount);

                if (!await DelayUnlessShuttingDownAsync(OutcomeWriteRetryDelays[attempt]))
                {
                    return OutcomeWrite.Unwritable;
                }
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Recording {Outcome} for background job {JobId} attempt {Attempt} failed",
                    outcome,
                    claimed.Id,
                    claimed.AttemptCount);
                return OutcomeWrite.Unwritable;
            }
        }

        return OutcomeWrite.Unwritable;
    }

    private async Task<bool> DelayUnlessShuttingDownAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, shutdownSignal.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<bool> WriteOutcomeAsync(
        ClaimedJob claimed,
        AttemptRowWrite write,
        BackgroundJobTypePolicy policy,
        DateTime nextAttemptAt,
        string? errorMessage,
        DateTime endedAt)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBackgroundJobStateRepository>();

        // Not the scheduler's token: an attempt that has already run has to be able to say how it ended.
        var attempt = new AttemptRef(claimed.Id, claimed.AttemptCount);
        return write switch
        {
            AttemptRowWrite.Completed =>
                await repository.TryCompleteAsync(attempt, endedAt, CancellationToken.None),
            AttemptRowWrite.Failed =>
                await repository.TryFailAsync(attempt, new AttemptFailure(errorMessage!, endedAt), CancellationToken.None),
            _ =>
                await repository.RecordFailedAttemptAsync(
                    attempt,
                    new RetryableFailure(new AttemptFailure(errorMessage!, endedAt), policy.MaxAttempts, nextAttemptAt),
                    CancellationToken.None),
        };
    }

    /// <summary>How the write that ends an attempt went.</summary>
    private enum OutcomeWrite
    {
        Landed,
        RowMovedOn,
        Unwritable,
    }

    private string ThrownMessage(ClaimedJob claimed, Exception thrown, bool runtimeReached)
    {
        if (runtimeReached && thrown is OperationCanceledException)
        {
            logger.LogWarning(
                "Background job {JobId} of type {JobType} attempt {Attempt} exceeded its maximum run time",
                claimed.Id,
                claimed.JobType,
                claimed.AttemptCount);
            return BackgroundJobMessages.RuntimeCeilingReached;
        }

        return SafeError.LogAndResolve(
            logger,
            thrown,
            BackgroundJobMessages.HandlerThrew,
            $"running background job {claimed.Id} ({claimed.JobType}) attempt {claimed.AttemptCount}, trace {claimed.TraceId}");
    }

    /// <summary>The message of a failure <see cref="Result"/>, joined as a problem response joins them.</summary>
    private static string FailureMessage(Result result)
    {
        if (NonBlank(result.Errors) is { Count: > 0 } errors)
        {
            return string.Join('\n', errors);
        }

        if (NonBlank(result.ValidationErrors?.Select(validationError => validationError?.ErrorMessage))
            is { Count: > 0 } validationMessages)
        {
            return string.Join('\n', validationMessages);
        }

        return BackgroundJobMessages.FailedWithoutMessage;
    }

    // A handler builds the failure Result itself, and one whose collection, or an entry in it, is missing has to
    // end the attempt as the failure it reported rather than as a throw.
    private static List<string> NonBlank(IEnumerable<string?>? messages) =>
        [.. (messages ?? []).OfType<string>().Where(message => !string.IsNullOrWhiteSpace(message))];

    // A metrics sink is host-supplied, so its failure is reported and dropped rather than allowed to change how a
    // job ended.
    private void Record(JobLifecycleEvent lifecycleEvent, string jobType)
    {
        try
        {
            metrics.Record(lifecycleEvent, jobType);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Recording {LifecycleEvent} for background job type {JobType} failed", lifecycleEvent, jobType);
        }
    }

    private void ObserveDuration(string jobType, DateTime claimedAt, JobAttemptOutcome outcome)
    {
        try
        {
            metrics.ObserveDuration(jobType, dateTimeProvider.UtcNow.UtcDateTime - claimedAt, outcome);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Observing a {Outcome} attempt of background job type {JobType} failed", outcome, jobType);
        }
    }
}
