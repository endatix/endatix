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
    IJobExecutionContextResolver contextResolver,
    IDateTimeProvider dateTimeProvider,
    IOptions<BackgroundJobsOptions> options,
    JobsShutdownSignal shutdownSignal,
    IJobMetrics metrics,
    ILogger<BackgroundJobExecution> logger) : IJob
{
    /// <summary>The trigger data key that holds the job row's id.</summary>
    public const string JobIdKey = "jobId";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var jobId = long.Parse(context.MergedJobDataMap.GetString(JobIdKey)!, CultureInfo.InvariantCulture);
        var claimedAt = dateTimeProvider.UtcNow.UtcDateTime;

        // A recovery re-claims the row and counts a new attempt, so without this a job that takes its node down
        // on its last attempt would run again on every recovery, past its budget and never dead-lettered.
        if (context.Recovering && await TryDeadLetterSpentAsync(jobId, context.JobDetail.Key.Name, claimedAt, cancellationToken))
        {
            return;
        }

        var claimed = await ClaimAsync(jobId, claimedAt, context.Recovering, cancellationToken);
        if (claimed is null)
        {
            // Returning tells the scheduler this firing is done: the job already ran, is running elsewhere, or
            // was cancelled before it started.
            return;
        }

        Record(JobLifecycleEvent.Claimed, claimed.JobType);

        var policy = options.Value.ResolvePolicy(claimed.JobType);
        var (end, result, thrown, runtimeReached) = await RunHandlerAsync(claimed, policy, cancellationToken);
        var decision = AttemptDecision.Decide(end, claimed.AttemptCount, policy.MaxAttempts);

        if (end is AttemptEnd.Canceled)
        {
            Record(JobLifecycleEvent.Canceled, claimed.JobType);
            ObserveDuration(claimed.JobType, claimedAt, JobAttemptOutcome.Canceled);
            return;
        }

        if (end is AttemptEnd.HostShutdown)
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

        var recorded = await RecordOutcomeAsync(claimed, claimedAt, decision.Row, policy, result, thrown, runtimeReached);

        if (decision.Rethrow && recorded)
        {
            // No inner exception: it was logged when its message was resolved, and the scheduler would log it
            // again.
            throw new JobExecutionException(
                $"Background job {claimed.Id} attempt {claimed.AttemptCount} failed and will be retried.");
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
                jobId,
                options.Value.ResolvePolicy(jobType).MaxAttempts,
                BackgroundJobMessages.StoppedOnLastAttempt,
                utcNow,
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
        return await repository.TryClaimAsync(jobId, registry.JobTypes, claimedAt, recovering, cancellationToken);
    }

    private async Task<(AttemptEnd End, Result? Result, Exception? Thrown, bool RuntimeReached)> RunHandlerAsync(
        ClaimedJob claimed,
        BackgroundJobTypePolicy policy,
        CancellationToken schedulerToken)
    {
        using var runtime = new CancellationTokenSource(policy.MaxRuntime);
        await using var watcher = CancellationWatcher.Start(
            scopeFactory,
            claimed.Id,
            TimeSpan.FromSeconds(options.Value.CancellationPollSeconds),
            logger);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            schedulerToken, shutdownSignal.Token, runtime.Token, watcher.Token);

        var jobContext = contextResolver.Resolve(claimed);
        using var activity = BackgroundJobsTelemetry.StartExecution(claimed);

        Result? result = null;
        Exception? thrown = null;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
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
        var succeeded = thrown is null && result!.IsSuccess;
        var end = watcher.SawCancellation ? AttemptEnd.Canceled
            : succeeded ? AttemptEnd.Succeeded
            : shutdownSignal.IsRaised || schedulerToken.IsCancellationRequested ? AttemptEnd.HostShutdown
            : thrown is not null ? AttemptEnd.Threw
            : AttemptEnd.ReturnedFailure;

        return (end, result, thrown, runtime.IsCancellationRequested);
    }

    /// <summary>
    /// Makes the one fenced write that ends the attempt, and records its event and duration only when that write
    /// landed: a lost write means another run owns the row, and an event recorded for it would count an attempt
    /// twice. Returns whether the write landed.
    /// </summary>
    private async Task<bool> RecordOutcomeAsync(
        ClaimedJob claimed,
        DateTime claimedAt,
        AttemptRowWrite write,
        BackgroundJobTypePolicy policy,
        Result? result,
        Exception? thrown,
        bool runtimeReached)
    {
        var endedAt = dateTimeProvider.UtcNow.UtcDateTime;
        var (lifecycleEvent, outcome) = write switch
        {
            AttemptRowWrite.Completed => (JobLifecycleEvent.Completed, JobAttemptOutcome.Completed),
            AttemptRowWrite.Failed => (JobLifecycleEvent.Failed, JobAttemptOutcome.Failed),
            AttemptRowWrite.Retrying => (JobLifecycleEvent.RetryScheduled, JobAttemptOutcome.RetryScheduled),
            _ => (JobLifecycleEvent.DeadLettered, JobAttemptOutcome.DeadLettered),
        };

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBackgroundJobStateRepository>();

            // Not the scheduler's token: an attempt that has already run has to be able to say how it ended.
            var landed = write switch
            {
                AttemptRowWrite.Completed =>
                    await repository.TryCompleteAsync(claimed.Id, claimed.AttemptCount, endedAt, CancellationToken.None),
                AttemptRowWrite.Failed =>
                    await repository.TryFailAsync(
                        claimed.Id, claimed.AttemptCount, FailureMessage(result!), endedAt, CancellationToken.None),
                _ =>
                    await repository.RecordFailedAttemptAsync(
                        claimed.Id,
                        claimed.AttemptCount,
                        policy.MaxAttempts,
                        BackgroundJobRetryPolicy.NextAttemptAt(claimed.AttemptCount, endedAt, policy),
                        ThrownMessage(claimed, thrown!, runtimeReached),
                        endedAt,
                        CancellationToken.None),
            };

            if (!landed)
            {
                logger.LogWarning(
                    "Background job {JobId} ended attempt {Attempt} as {Outcome}, but the row had moved on and was left as it is",
                    claimed.Id,
                    claimed.AttemptCount,
                    outcome);
                return false;
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
            return false;
        }

        Record(lifecycleEvent, claimed.JobType);
        ObserveDuration(claimed.JobType, claimedAt, outcome);
        return true;
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
