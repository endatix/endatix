using System.Diagnostics;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Runs a claimed attempt's handler under every limit that can stop it — the scheduler's token, host shutdown,
/// the runtime ceiling and the row's own cancellation — and says how the run ended.
/// </summary>
internal sealed class JobHandlerRunner(
    IServiceScopeFactory scopeFactory,
    JobHandlerRegistry registry,
    IOptions<BackgroundJobsOptions> options,
    JobsShutdownSignal shutdownSignal,
    ILogger<JobHandlerRunner> logger)
{
    public async Task<HandlerRun> RunAsync(ClaimedAttempt attempt, CancellationToken schedulerToken)
    {
        using var runtime = new CancellationTokenSource(attempt.Policy.MaxRuntime);
        await using var watcher = CancellationWatcher.Start(scopeFactory, WatchedAttemptOf(attempt), logger);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            schedulerToken, shutdownSignal.Token, runtime.Token, watcher.Token);

        var invocation = await InvokeAsync(attempt.Job, linked.Token);

        // A handler that returned success did its work whenever the shutdown came, so it is recorded: leaving it
        // for recovery would run that work a second time.
        var end = DecideEnd(new HandlerRunSignals(
            RowCanceled: watcher.SawCancellation,
            RowSuperseded: watcher.SawSupersession,
            Succeeded: invocation.Result is { IsSuccess: true },
            ShuttingDown: shutdownSignal.IsRaised || schedulerToken.IsCancellationRequested,
            Threw: invocation.Thrown is not null));

        return new HandlerRun(end, invocation.Result, invocation.Thrown, runtime.IsCancellationRequested);
    }

    /// <summary>
    /// Checked in this order: a cancelled or superseded row wins over whatever the handler did, and success wins
    /// over a shutdown that arrived after the work was done.
    /// </summary>
    internal static AttemptEnd DecideEnd(HandlerRunSignals signals) => signals switch
    {
        { RowCanceled: true } => AttemptEnd.Canceled,
        { RowSuperseded: true } => AttemptEnd.Superseded,
        { Succeeded: true } => AttemptEnd.Succeeded,
        { ShuttingDown: true } => AttemptEnd.HostShutdown,
        { Threw: true } => AttemptEnd.Threw,
        _ => AttemptEnd.ReturnedFailure,
    };

    private WatchedAttempt WatchedAttemptOf(ClaimedAttempt attempt) =>
        new(attempt.Ref, TimeSpan.FromSeconds(options.Value.CancellationPollSeconds));

    private async Task<HandlerInvocation> InvokeAsync(ClaimedJob job, CancellationToken cancellationToken)
    {
        using var activity = BackgroundJobsTelemetry.StartExecution(job);
        try
        {
            return new HandlerInvocation(await ExecuteHandlerAsync(job, activity, cancellationToken), null);
        }
        catch (Exception exception)
        {
            // The type name only, because a message can carry secrets, which is why none reaches the row either.
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            return new HandlerInvocation(null, exception);
        }
    }

    private async Task<Result> ExecuteHandlerAsync(ClaimedJob job, Activity? activity, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var jobContext = scope.ServiceProvider.GetRequiredService<IJobExecutionContextResolver>().Resolve(job);
        var handler = registry.Resolve(scope.ServiceProvider, job.JobType)
            ?? throw new InvalidOperationException(
                $"No background job handler is registered for job type '{job.JobType}'.");

        var result = await handler.ExecuteAsync(jobContext, cancellationToken);
        if (!result.IsSuccess)
        {
            // No description: the message the handler wrote goes on the row.
            activity?.SetStatus(ActivityStatusCode.Error);
        }

        return result;
    }

    private sealed record HandlerInvocation(Result? Result, Exception? Thrown);
}

/// <summary>How the handler's run ended, and what it returned or threw.</summary>
internal sealed record HandlerRun(AttemptEnd End, Result? Result, Exception? Thrown, bool RuntimeReached);

/// <summary>What the wrapper saw when a handler's run ended.</summary>
/// <param name="RowCanceled">The row was seen <c>Canceled</c> while the handler ran.</param>
/// <param name="RowSuperseded">The row was seen to belong to another attempt, or to be gone.</param>
/// <param name="Succeeded">The handler returned success.</param>
/// <param name="ShuttingDown">The host or the scheduler was stopping when the handler ended.</param>
/// <param name="Threw">The handler threw rather than returned.</param>
internal readonly record struct HandlerRunSignals(
    bool RowCanceled,
    bool RowSuperseded,
    bool Succeeded,
    bool ShuttingDown,
    bool Threw);
