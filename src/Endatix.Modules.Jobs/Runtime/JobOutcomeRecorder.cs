using Endatix.Core.Abstractions;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Result;
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
    IDateTimeProvider dateTimeProvider,
    JobsShutdownSignal shutdownSignal,
    JobTypePolicies policies,
    JobLifecycleMetrics metrics,
    ReadOnlyDatabaseWarning readOnlyWarning,
    OutcomeRowWriter writer,
    ILogger<JobOutcomeRecorder> logger)
{
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
        var tried = await WriteAndCountAsync(plan);
        var carried = tried.Written is OutcomeWrite.Unwritable ? plan.Unsaved : null;
        WarnIfCarriedPastReadOnly(tried, carried is not null);
        return new(tried.Written, carried);
    }

    /// <summary>
    /// Writes the outcome of an attempt of <paramref name="job"/> whose own write failed, fenced on that attempt, so a
    /// row that has moved on since — cancelled, taken over or already recorded — is left as it is. Its duration runs to
    /// when the attempt ended, however much later this write lands. A write that cannot be made is
    /// <see cref="OutcomeWrite.Rejected"/> when the database answered all the same, and
    /// <see cref="OutcomeWrite.Unwritable"/> otherwise.
    /// </summary>
    public async Task<OutcomeWrite> RecordUnsavedAsync(ReclaimableJob job, UnsavedOutcome unsaved)
    {
        var attempt = new EndedAttempt(
            new AttemptRef(job.JobId, unsaved.Attempt), job.JobType, policies.For(job.JobType), unsaved.ClaimedAt);
        var plan = new OutcomeWritePlan(attempt, unsaved.Write, unsaved.ErrorMessage, unsaved.EndedAt);
        var tried = await WriteAndCountAsync(plan);
        if (tried.Written is OutcomeWrite.Landed)
        {
            LogRecordedLate(plan.Attempt.Ref, plan.Outcome);
        }

        var written = tried.Failure is { } failure && await writer.IsRejectedAsync(job.JobId, failure)
            ? OutcomeWrite.Rejected
            : tried.Written;
        WarnIfCarriedPastReadOnly(tried, written is OutcomeWrite.Unwritable);
        return written;
    }

    // Nothing is worked out, nor boxed, for a log that is off (CA1873).
    private void LogRecordedLate(AttemptRef attempt, JobAttemptOutcome outcome)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        logger.LogInformation(
            "Background job {JobId} attempt {Attempt} had its outcome {Outcome} recorded after the attempt's own write failed",
            attempt.JobId,
            attempt.AttemptCount,
            outcome);
    }

    // Says why an outcome carried to the next firing could not be written, when a read-only database refused it. A
    // stopping host carries nothing on, so it says nothing.
    private void WarnIfCarriedPastReadOnly(WriteTry tried, bool carried)
    {
        if (carried && !shutdownSignal.IsRaised && tried.Failure is { } failure)
        {
            readOnlyWarning.WarnIfReadOnly(failure);
        }
    }

    /// <summary>
    /// Logs the throw that ended an attempt for which nothing is recorded, as recording it would have; with no write
    /// landed, no lifecycle event or duration is recorded either.
    /// </summary>
    public void LogUnrecordedThrow(ClaimedAttempt attempt, HandlerRun run) => _ = ThrownMessage(attempt.Job, run);

    // The event and duration are recorded once per attempt, by whichever firing's write lands, and the duration runs to
    // when the attempt ended, not to when its outcome was written.
    private async Task<WriteTry> WriteAndCountAsync(OutcomeWritePlan plan)
    {
        var tried = await writer.WriteWithRetriesAsync(plan);
        if (tried.Written is OutcomeWrite.Landed)
        {
            metrics.RecordEnd(plan.Attempt, plan.Outcome, plan.EndedAt);
        }

        return tried;
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
    /// The write kept throwing while the database answered: the database refused it, and trying again does not clear
    /// that. Only the write of an outcome a re-fire carries is told apart from <see cref="Unwritable"/>.
    /// </summary>
    Rejected,

    /// <summary>
    /// The write was not made, because the trigger of the next attempt could not be stored; the row stays
    /// <c>Processing</c>.
    /// </summary>
    Unscheduled,
}
