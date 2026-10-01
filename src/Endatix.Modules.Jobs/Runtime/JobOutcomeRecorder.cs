using Endatix.Core.Abstractions;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Records how an attempt ended: the one fenced write to its row, and the lifecycle event and duration.
/// </summary>
/// <remarks>
/// The event and duration are recorded only when the write landed: a lost write means another run owns the row,
/// and an event recorded for it would count an attempt twice. A write that throws is tried again a few times,
/// because a database that blinked would otherwise lose the outcome of work that is done.
/// </remarks>
internal sealed class JobOutcomeRecorder(
    IServiceScopeFactory scopeFactory,
    IDateTimeProvider dateTimeProvider,
    JobsShutdownSignal shutdownSignal,
    JobLifecycleMetrics metrics,
    ILogger<JobOutcomeRecorder> logger)
{
    /// <summary>How long the recorder waits before each further try at an outcome write that failed.</summary>
    internal static readonly TimeSpan[] WriteRetryDelays =
        [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    public async Task<OutcomeWrite> RecordAsync(ClaimedAttempt attempt, AttemptEnding ending)
    {
        if (ending.Write is AttemptRowWrite.None)
        {
            RecordUnwritten(attempt, ending.Run.End);
            return OutcomeWrite.NothingToWrite;
        }

        var plan = PlanWrite(attempt, ending);
        var written = await WriteWithRetriesAsync(plan);
        if (written is OutcomeWrite.Landed)
        {
            metrics.RecordEnd(attempt, LifecycleEventOf(plan.Outcome), plan.Outcome);
        }

        return written;
    }

    /// <summary>
    /// Logs the throw that ended an attempt for which nothing is recorded, as recording it would have; with no write
    /// landed, no lifecycle event or duration is recorded either.
    /// </summary>
    public void LogUnrecordedThrow(ClaimedAttempt attempt, HandlerRun run) => _ = ThrownMessage(attempt.Job, run);

    // An end that writes nothing leaves the row as it is: a cancelled row keeps its status, and a superseded or
    // abandoned attempt's row belongs to whichever attempt runs next.
    private void RecordUnwritten(ClaimedAttempt attempt, AttemptEnd end)
    {
        if (end is AttemptEnd.Canceled)
        {
            metrics.RecordEnd(attempt, JobLifecycleEvent.Canceled, JobAttemptOutcome.Canceled);
            return;
        }

        LogAbandoned(attempt.Job, end);
        metrics.RecordEnd(attempt, JobLifecycleEvent.Abandoned, JobAttemptOutcome.Abandoned);
    }

    private void LogAbandoned(ClaimedJob job, AttemptEnd end)
    {
        if (end is AttemptEnd.Superseded)
        {
            // Another attempt owns the row, so nothing this one writes could land; its handler was stopped so the
            // two do not run the work side by side.
            logger.LogWarning(
                "Background job {JobId} attempt {Attempt} was taken over by another attempt; its handler was stopped and nothing is recorded",
                job.Id,
                job.AttemptCount);
            return;
        }

        // Indistinguishable from a crash at the same instant, so it is treated like one: the attempt taken at the
        // claim stands, nothing is recorded, and the job runs again on the next node to check in.
        logger.LogInformation(
            "Background job {JobId} attempt {Attempt} was left running at shutdown and will run again",
            job.Id,
            job.AttemptCount);
    }

    // The message is resolved once, before any try, because resolving a thrown message logs the exception.
    private OutcomeWritePlan PlanWrite(ClaimedAttempt attempt, AttemptEnding ending)
    {
        var errorMessage = ending.Write switch
        {
            AttemptRowWrite.Completed => null,
            AttemptRowWrite.Failed => FailureMessage(ending.Run.Result!),
            _ => ThrownMessage(attempt.Job, ending.Run),
        };

        return new OutcomeWritePlan(attempt, ending, errorMessage, dateTimeProvider.UtcNow.UtcDateTime);
    }

    private async Task<OutcomeWrite> WriteWithRetriesAsync(OutcomeWritePlan plan)
    {
        // One first try, then one more after each delay.
        for (var retry = 0; retry <= WriteRetryDelays.Length; retry++)
        {
            var tried = await WriteOnceAsync(plan);
            if (tried.Failure is null)
            {
                return tried.Written;
            }

            if (!await WaitToRetryAsync(plan, tried.Failure, retry))
            {
                return OutcomeWrite.Unwritable;
            }
        }

        return OutcomeWrite.Unwritable;
    }

    private async Task<WriteTry> WriteOnceAsync(OutcomeWritePlan plan)
    {
        try
        {
            return new WriteTry(await WriteAsync(plan) ? OutcomeWrite.Landed : RowMovedOn(plan), null);
        }
        catch (Exception exception)
        {
            return new WriteTry(OutcomeWrite.Unwritable, exception);
        }
    }

    private OutcomeWrite RowMovedOn(OutcomeWritePlan plan)
    {
        logger.LogWarning(
            "Background job {JobId} ended attempt {Attempt} as {Outcome}, but the row had moved on and was left as it is",
            plan.Attempt.Job.Id,
            plan.Attempt.Job.AttemptCount,
            plan.Outcome);
        return OutcomeWrite.RowMovedOn;
    }

    // Logs the failed try, and waits for the next one unless that was the last try or the host is shutting down.
    private async Task<bool> WaitToRetryAsync(OutcomeWritePlan plan, Exception failure, int retry)
    {
        var job = plan.Attempt.Job;
        if (retry == WriteRetryDelays.Length || shutdownSignal.IsRaised)
        {
            logger.LogError(
                failure, "Recording {Outcome} for background job {JobId} attempt {Attempt} failed", plan.Outcome, job.Id, job.AttemptCount);
            return false;
        }

        logger.LogWarning(
            failure,
            "Recording {Outcome} for background job {JobId} attempt {Attempt} failed; trying again",
            plan.Outcome,
            job.Id,
            job.AttemptCount);
        return await DelayUnlessShuttingDownAsync(WriteRetryDelays[retry]);
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

    // Not the scheduler's token: an attempt that has already run has to be able to say how it ended.
    private Task<bool> WriteAsync(OutcomeWritePlan plan) =>
        scopeFactory.WithStateRepositoryAsync(repository => plan.Ending.Write switch
        {
            AttemptRowWrite.Completed =>
                repository.TryCompleteAsync(plan.Attempt.Ref, plan.Finish, CancellationToken.None),
            AttemptRowWrite.Failed =>
                repository.TryFailAsync(plan.Attempt.Ref, plan.Failure, CancellationToken.None),
            _ =>
                repository.RecordFailedAttemptAsync(plan.Attempt.Ref, plan.RetryableFailure, CancellationToken.None),
        });

    private string ThrownMessage(ClaimedJob job, HandlerRun run)
    {
        if (run.RuntimeReached && run.Thrown is OperationCanceledException)
        {
            logger.LogWarning(
                "Background job {JobId} of type {JobType} attempt {Attempt} exceeded its maximum run time",
                job.Id,
                job.JobType,
                job.AttemptCount);
            return BackgroundJobMessages.RuntimeCeilingReached;
        }

        return SafeError.LogAndResolve(
            logger,
            run.Thrown!,
            BackgroundJobMessages.HandlerThrew,
            $"running background job {job.Id} ({job.JobType}) attempt {job.AttemptCount}, trace {job.TraceId}");
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

    private static JobLifecycleEvent LifecycleEventOf(JobAttemptOutcome outcome) => outcome switch
    {
        JobAttemptOutcome.Completed => JobLifecycleEvent.Completed,
        JobAttemptOutcome.Failed => JobLifecycleEvent.Failed,
        JobAttemptOutcome.RetryScheduled => JobLifecycleEvent.RetryScheduled,
        _ => JobLifecycleEvent.DeadLettered,
    };

    /// <summary>One try at the write: how it went, or what it threw.</summary>
    private readonly record struct WriteTry(OutcomeWrite Written, Exception? Failure);

    /// <summary>The write that ends an attempt, with everything each try at it needs.</summary>
    private sealed record OutcomeWritePlan(
        ClaimedAttempt Attempt,
        AttemptEnding Ending,
        string? ErrorMessage,
        DateTime EndedAt)
    {
        public JobAttemptOutcome Outcome => Ending.Write switch
        {
            AttemptRowWrite.Completed => JobAttemptOutcome.Completed,
            AttemptRowWrite.Failed => JobAttemptOutcome.Failed,
            AttemptRowWrite.Retrying => JobAttemptOutcome.RetryScheduled,
            _ => JobAttemptOutcome.DeadLettered,
        };

        public JobFinish Finish => new(EndedAt, Attempt.Policy.Retention);

        public AttemptFailure Failure => new(ErrorMessage!, EndedAt, Attempt.Policy.Retention);

        public RetryableFailure RetryableFailure =>
            new(Failure, Attempt.Policy.MaxAttempts, Ending.NextAttemptAt);
    }
}

/// <summary>How an attempt ends: the write it makes, the run it records, and when a retry would be due.</summary>
internal sealed record AttemptEnding(AttemptRowWrite Write, HandlerRun Run, DateTime NextAttemptAt);

/// <summary>How the write that ends an attempt went.</summary>
internal enum OutcomeWrite
{
    /// <summary>The attempt's end writes nothing to the row.</summary>
    NothingToWrite,
    Landed,
    RowMovedOn,
    Unwritable,

    /// <summary>
    /// The write was not made, because the trigger of the next attempt could not be stored; the row stays
    /// <c>Processing</c>.
    /// </summary>
    Unscheduled,
}
