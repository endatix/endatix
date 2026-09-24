using System.Diagnostics;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Runs one claimed attempt of one job and records how it ended.
/// </summary>
/// <remarks>
/// <para>
/// Nothing reaches the caller, not even a write that failed: the dispatch loop holds a slot, not an opinion about
/// a job, and a row whose outcome could not be written is left for the sweeper to reap rather than retried here,
/// which would need an attempt this one already consumed.
/// </para>
/// <para>
/// The claim, the handler and each outcome write each run in a scope of their own, because a <c>DbContext</c> is
/// neither thread-safe nor meant to be held for the length of a job.
/// </para>
/// </remarks>
internal sealed class BackgroundJobExecutor(
    IServiceScopeFactory scopeFactory,
    BackgroundJobHandlerRegistry handlerRegistry,
    IJobExecutionContextResolver contextResolver,
    IDateTimeProvider dateTimeProvider,
    IOptions<BackgroundJobsOptions> options,
    IJobMetrics metrics,
    ILogger<BackgroundJobExecutor> logger)
{
    public async Task RunAsync(JobDispatchItem item, CancellationToken stoppingToken)
    {
        var claimedAt = dateTimeProvider.UtcNow.UtcDateTime;

        var claimed = await TryClaimAsync(item, claimedAt, stoppingToken);
        if (claimed is null)
        {
            return;
        }

        Record(JobLifecycleEvent.Claimed, claimed.JobType);

        try
        {
            var result = await InvokeHandlerAsync(claimed, stoppingToken);
            await RecordResultAsync(claimed, claimedAt, result);
        }
        catch (Exception exception)
        {
            await RecordThrownAsync(claimed, claimedAt, exception);
        }
    }

    /// <summary>
    /// The claim, or <see langword="null"/> when this instance did not get the attempt — the job was claimed,
    /// cancelled or reaped between the read that offered it and the update, and whoever did that owns it now.
    /// </summary>
    private async Task<ClaimedJob?> TryClaimAsync(
        JobDispatchItem item,
        DateTime claimedAt,
        CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBackgroundJobStateRepository>();

            var claimed = await repository.TryClaimAsync(
                item.JobId, handlerRegistry.JobTypes, claimedAt, stoppingToken);

            if (claimed is null)
            {
                logger.LogDebug(
                    "Background job {JobId} of type {JobType} was no longer claimable",
                    item.JobId,
                    item.JobType);
            }

            return claimed;
        }
        catch (Exception exception)
        {
            // The row is untouched, so the next sweep offers it again.
            logger.LogError(
                exception,
                "Claiming background job {JobId} of type {JobType} failed",
                item.JobId,
                item.JobType);

            return null;
        }
    }

    private async Task<Result> InvokeHandlerAsync(ClaimedJob claimed, CancellationToken stoppingToken)
    {
        var context = contextResolver.Resolve(claimed);

        using var activity = BackgroundJobsTelemetry.StartExecution(claimed);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = handlerRegistry.Resolve(scope.ServiceProvider, claimed.JobType);

            var result = await handler.ExecuteAsync(context, stoppingToken);
            if (!result.IsSuccess)
            {
                // No description: the message the handler wrote is already on the row.
                activity?.SetStatus(ActivityStatusCode.Error);
            }

            return result;
        }
        catch (Exception exception)
        {
            // The type name only, because a message can carry secrets, which is why none reaches the row either.
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
    }

    private Task RecordResultAsync(ClaimedJob claimed, DateTime claimedAt, Result result)
    {
        if (result.IsSuccess)
        {
            return WriteOutcomeAsync(
                claimed,
                claimedAt,
                JobAttemptOutcome.Completed,
                (repository, utcNow, cancellationToken) =>
                    repository.TryCompleteAsync(claimed.Id, claimed.AttemptCount, utcNow, cancellationToken));
        }

        // A handler that returns rather than throws reports a failure that repeating cannot fix, so the job goes
        // terminal on this attempt however many it had left.
        var failureMessage = FailureMessage(result);

        return WriteOutcomeAsync(
            claimed,
            claimedAt,
            JobAttemptOutcome.Failed,
            (repository, utcNow, cancellationToken) => repository.TryFailAsync(
                claimed.Id, claimed.AttemptCount, failureMessage, utcNow, cancellationToken));
    }

    private Task RecordThrownAsync(ClaimedJob claimed, DateTime claimedAt, Exception exception)
    {
        var resolvedMessage = SafeError.LogAndResolve(
            logger,
            exception,
            BackgroundJobMessages.HandlerThrew,
            $"running background job {claimed.Id} ({claimed.JobType}) attempt {claimed.AttemptCount}, " +
            $"trace {claimed.TraceId}");

        var policy = options.Value.ResolvePolicy(claimed.JobType);

        // The write decides the row's status from this same comparison; it is repeated here to name the event the
        // attempt ended with.
        var outcome = claimed.AttemptCount >= policy.MaxAttempts
            ? JobAttemptOutcome.DeadLettered
            : JobAttemptOutcome.RetryScheduled;

        return WriteOutcomeAsync(
            claimed,
            claimedAt,
            outcome,
            (repository, utcNow, cancellationToken) => repository.RecordFailedAttemptAsync(
                claimed.Id,
                claimed.AttemptCount,
                policy.MaxAttempts,
                BackgroundJobRetryPolicy.NextAttemptAt(claimed.AttemptCount, utcNow, policy),
                resolvedMessage,
                utcNow,
                cancellationToken));
    }

    /// <summary>
    /// Makes the one fenced write that ends the attempt, and records the attempt's event and duration only when
    /// that write landed: a lost write means another instance owns the row, and an event recorded for it would
    /// count an attempt twice.
    /// </summary>
    private async Task WriteOutcomeAsync(
        ClaimedJob claimed,
        DateTime claimedAt,
        JobAttemptOutcome outcome,
        Func<IBackgroundJobStateRepository, DateTime, CancellationToken, Task<bool>> write)
    {
        var endedAt = dateTimeProvider.UtcNow.UtcDateTime;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBackgroundJobStateRepository>();

            // Not the host's stopping token: an attempt that has already run has to be able to say how it ended.
            if (!await write(repository, endedAt, CancellationToken.None))
            {
                logger.LogWarning(
                    "Background job {JobId} ended attempt {Attempt} as {Outcome}, but the row had moved on and was left as it is",
                    claimed.Id,
                    claimed.AttemptCount,
                    outcome);

                return;
            }
        }
        catch (Exception exception)
        {
            // The row stays Processing without a heartbeat, which is what the sweeper reaps.
            logger.LogError(
                exception,
                "Recording {Outcome} for background job {JobId} attempt {Attempt} failed",
                outcome,
                claimed.Id,
                claimed.AttemptCount);

            return;
        }

        Record(LifecycleEventOf(outcome), claimed.JobType);
        ObserveDuration(claimed.JobType, endedAt - claimedAt, outcome);
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

    // Null-tolerant, and read that way at both call sites: a handler builds the failure Result itself, and one
    // whose collection, or an entry in it, is missing has to end the attempt as the failure it reported rather than
    // as a throw.
    private static List<string> NonBlank(IEnumerable<string?>? messages) =>
        [.. (messages ?? []).OfType<string>().Where(message => !string.IsNullOrWhiteSpace(message))];

    private static JobLifecycleEvent LifecycleEventOf(JobAttemptOutcome outcome) => outcome switch
    {
        JobAttemptOutcome.Completed => JobLifecycleEvent.Completed,
        JobAttemptOutcome.Failed => JobLifecycleEvent.Failed,
        JobAttemptOutcome.RetryScheduled => JobLifecycleEvent.RetryScheduled,
        JobAttemptOutcome.DeadLettered => JobLifecycleEvent.DeadLettered,
        JobAttemptOutcome.Canceled => JobLifecycleEvent.Canceled,
        JobAttemptOutcome.Abandoned => JobLifecycleEvent.Abandoned,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

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
            logger.LogDebug(
                exception,
                "Recording {LifecycleEvent} for background job type {JobType} failed",
                lifecycleEvent,
                jobType);
        }
    }

    private void ObserveDuration(string jobType, TimeSpan duration, JobAttemptOutcome outcome)
    {
        try
        {
            metrics.ObserveDuration(jobType, duration, outcome);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "Observing the duration of a {Outcome} attempt of background job type {JobType} failed",
                outcome,
                jobType);
        }
    }
}
