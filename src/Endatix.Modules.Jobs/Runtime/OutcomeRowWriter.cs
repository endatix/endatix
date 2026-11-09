using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The fenced write that ends an attempt, tried again a few times while it throws, and whether a write that kept
/// throwing was refused by a database that answered rather than lost to an outage.
/// </summary>
internal sealed class OutcomeRowWriter(
    IServiceScopeFactory scopeFactory,
    JobsShutdownSignal shutdownSignal,
    OutcomeWriteRetries retries,
    ILogger<OutcomeRowWriter> logger)
{
    // Returns the last try: how it went, or, when the write could not be made, what that try threw.
    public async Task<WriteTry> WriteWithRetriesAsync(OutcomeWritePlan plan)
    {
        var tried = default(WriteTry);
        for (var retry = 0; retry < retries.Tries; retry++)
        {
            tried = await WriteOnceAsync(plan);
            if (tried.Failure is null || !await WaitToRetryAsync(plan, tried.Failure, retry))
            {
                return tried;
            }
        }

        return tried;
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
        if (retry == retries.Delays.Count || shutdownSignal.IsRaised)
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
        return await DelayUnlessShuttingDownAsync(retries.Delays[retry]);
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

    /// <summary>
    /// Whether a write that kept throwing with <paramref name="failure"/> was refused by a database that is up — by a
    /// constraint or a trigger, say — which trying again does not clear, rather than lost to an outage, which does.
    /// </summary>
    /// <remarks>
    /// A failure <see cref="DatabaseFailures.IsOutage"/> names — a timeout, a deadlock, a failover, a stopping server,
    /// a lost connection — is an outage, and so is any failure once the host is stopping. Anything else is told apart
    /// by reading the job's row: a read that lands means the database answered. No list of codes is complete, so the
    /// read is what keeps an outage it misses from counting.
    /// </remarks>
    public async Task<bool> IsRejectedAsync(long jobId, Exception failure)
    {
        if (shutdownSignal.IsRaised || DatabaseFailures.IsOutage(failure))
        {
            return false;
        }

        try
        {
            await scopeFactory.WithStateRepositoryAsync(repository => repository.ReadAttemptAsync(jobId, CancellationToken.None));
            return true;
        }
        catch (Exception readFailure) // Any failure: the database did not answer, so the write is not judged by it.
        {
            LogUnread(jobId, readFailure);
            return false;
        }
    }

    // Nothing is boxed for a log that is off (CA1873).
    private void LogUnread(long jobId, Exception readFailure)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(readFailure, "Background job {JobId} could not be read after its outcome write failed", jobId);
        }
    }
}

/// <summary>One try at the write: how it went, or what it threw.</summary>
internal readonly record struct WriteTry(OutcomeWrite Written, Exception? Failure);

/// <summary>The write that ends an attempt, with everything each try at it needs.</summary>
internal sealed record OutcomeWritePlan(
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

/// <summary>How long the writer waits before each further try at an outcome write that failed.</summary>
internal sealed record OutcomeWriteRetries(IReadOnlyList<TimeSpan> Delays)
{
    public static readonly OutcomeWriteRetries Default =
        new([TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)]);

    /// <summary>Every try at one write: a first, then one more after each delay.</summary>
    public int Tries => Delays.Count + 1;
}
