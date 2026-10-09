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
/// because a database that blinked would otherwise lose the outcome of work that is done. An outcome the handler
/// decided that still cannot be written is handed back, for the job's re-fire to carry and write.
/// </remarks>
internal sealed class JobOutcomeRecorder(
    IServiceScopeFactory scopeFactory,
    IDateTimeProvider dateTimeProvider,
    JobsShutdownSignal shutdownSignal,
    JobTypePolicies policies,
    JobLifecycleMetrics metrics,
    ILogger<JobOutcomeRecorder> logger)
{
    /// <summary>How long the recorder waits before each further try at an outcome write that failed.</summary>
    internal static readonly TimeSpan[] WriteRetryDelays =
        [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    /// <summary>
    /// Records how <paramref name="attempt"/> ended. When the write cannot be made, the result carries the outcome the
    /// handler decided, if it decided one, for the re-fire to write.
    /// </summary>
    public async Task<OutcomeRecording> RecordAsync(ClaimedAttempt attempt, AttemptEnding ending)
    {
        if (ending.Write is AttemptRowWrite.None)
        {
            RecordUnwritten(attempt, ending.Run.End);
            return new(OutcomeWrite.NothingToWrite);
        }

        var plan = PlanWrite(attempt, ending);
        var written = await WriteAndCountAsync(plan);
        return new(written, written is OutcomeWrite.Unwritable ? plan.Unsaved : null);
    }

    /// <summary>
    /// Writes the outcome of an attempt of <paramref name="job"/> whose own write failed, fenced on that attempt, so a
    /// row that has moved on since — cancelled, taken over or already recorded — is left as it is. Its duration runs to
    /// when the attempt ended, however much later this write lands.
    /// </summary>
    public async Task<OutcomeWrite> RecordUnsavedAsync(ReclaimableJob job, UnsavedOutcome unsaved)
    {
        var attempt = new EndedAttempt(
            new AttemptRef(job.JobId, unsaved.Attempt), job.JobType, policies.For(job.JobType), unsaved.ClaimedAt);
        var plan = new OutcomeWritePlan(attempt, unsaved.Write, unsaved.ErrorMessage, unsaved.EndedAt);
        var written = await WriteAndCountAsync(plan);
        if (written is OutcomeWrite.Landed)
        {
            logger.LogInformation(
                "Background job {JobId} attempt {Attempt} had its outcome {Outcome} recorded after the attempt's own write failed",
                job.JobId,
                unsaved.Attempt,
                plan.Outcome);
        }

        return written;
    }

    /// <summary>
    /// Logs the throw that ended an attempt for which nothing is recorded, as recording it would have; with no write
    /// landed, no lifecycle event or duration is recorded either.
    /// </summary>
    public void LogUnrecordedThrow(ClaimedAttempt attempt, HandlerRun run) => _ = ThrownMessage(attempt.Job, run);

    // The event and duration are recorded once per attempt, by whichever firing's write lands, and the duration runs to
    // when the attempt ended, not to when its outcome was written.
    private async Task<OutcomeWrite> WriteAndCountAsync(OutcomeWritePlan plan)
    {
        var written = await WriteWithRetriesAsync(plan);
        if (written is OutcomeWrite.Landed)
        {
            metrics.RecordEnd(plan.Attempt, plan.Outcome, plan.EndedAt);
        }

        return written;
    }

    // An end that writes nothing leaves the row as it is: a cancelled row keeps its status, and a superseded or
    // abandoned attempt's row belongs to whichever attempt runs next.
    private void RecordUnwritten(ClaimedAttempt attempt, AttemptEnd end)
    {
        var endedAt = dateTimeProvider.UtcNow.UtcDateTime;
        if (end is AttemptEnd.Canceled)
        {
            metrics.RecordEnd(attempt.Ended, JobAttemptOutcome.Canceled, endedAt);
            return;
        }

        LogAbandoned(attempt.Job, end);
        metrics.RecordEnd(attempt.Ended, JobAttemptOutcome.Abandoned, endedAt);
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

        return new OutcomeWritePlan(
            attempt.Ended, ending.Write, errorMessage, dateTimeProvider.UtcNow.UtcDateTime, ending.NextAttemptAt);
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
            plan.Attempt.Ref.JobId,
            plan.Attempt.Ref.AttemptCount,
            plan.Outcome);
        return OutcomeWrite.RowMovedOn;
    }

    // Logs the failed try, and waits for the next one unless that was the last try or the host is shutting down.
    private async Task<bool> WaitToRetryAsync(OutcomeWritePlan plan, Exception failure, int retry)
    {
        var attempt = plan.Attempt.Ref;
        if (retry == WriteRetryDelays.Length || shutdownSignal.IsRaised)
        {
            logger.LogError(
                failure, "Recording {Outcome} for background job {JobId} attempt {Attempt} failed", plan.Outcome, attempt.JobId, attempt.AttemptCount);
            return false;
        }

        logger.LogWarning(
            failure,
            "Recording {Outcome} for background job {JobId} attempt {Attempt} failed; trying again",
            plan.Outcome,
            attempt.JobId,
            attempt.AttemptCount);
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
        scopeFactory.WithStateRepositoryAsync(repository => plan.Write switch
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

    /// <summary>One try at the write: how it went, or what it threw.</summary>
    private readonly record struct WriteTry(OutcomeWrite Written, Exception? Failure);

    /// <summary>The write that ends an attempt, with everything each try at it needs.</summary>
    private sealed record OutcomeWritePlan(
        EndedAttempt Attempt,
        AttemptRowWrite Write,
        string? ErrorMessage,
        DateTime EndedAt,
        DateTime NextAttemptAt = default)
    {
        public JobAttemptOutcome Outcome => Write switch
        {
            AttemptRowWrite.Completed => JobAttemptOutcome.Completed,
            AttemptRowWrite.Failed => JobAttemptOutcome.Failed,
            AttemptRowWrite.Retrying => JobAttemptOutcome.RetryScheduled,
            _ => JobAttemptOutcome.DeadLettered,
        };

        public JobFinish Finish => new(EndedAt, Attempt.Policy.Retention);

        public AttemptFailure Failure => new(ErrorMessage!, EndedAt, Attempt.Policy.Retention);

        public RetryableFailure RetryableFailure =>
            new(Failure, Attempt.Policy.MaxAttempts, NextAttemptAt);

        /// <summary>The outcome for a re-fire to write, when the handler decided it.</summary>
        public UnsavedOutcome? Unsaved => UnsavedOutcome.Carries(Write)
            ? new UnsavedOutcome(Attempt.Ref.AttemptCount, Write, ErrorMessage, Attempt.ClaimedAt, EndedAt)
            : null;
    }
}

/// <summary>
/// How recording an attempt's outcome went, and, when it could not be written, the outcome for a re-fire to write.
/// </summary>
internal readonly record struct OutcomeRecording(OutcomeWrite Written, UnsavedOutcome? Unsaved = null);

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
