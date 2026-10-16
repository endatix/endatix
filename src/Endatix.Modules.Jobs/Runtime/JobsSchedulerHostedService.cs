using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Starts and stops the jobs scheduler with the host.
/// </summary>
/// <remarks>
/// <para>
/// Registered after the migration service, so the scheduler validates its tables only once they exist. Building
/// the scheduler is what validates them, which is why a schedule-only host builds it too: a node whose tables are
/// missing or outdated fails at startup rather than at its first enqueue.
/// </para>
/// <para>
/// A database that cannot be reached at all is a different failure. The host stays up — its readiness probe
/// reports the outage — and the scheduler starts once the database answers, because restarting the process
/// cannot fix a database. That holds for an outage at any step of the start, not only the first connection. A
/// database that answers and refuses, such as a login it does not accept, fails the start instead: waiting would
/// never fix it.
/// </para>
/// <para>
/// A SQL Server node set to enqueue without the trigger lock is refused before its scheduler is built unless the
/// database reads committed snapshots (<see cref="ReadCommittedSnapshotGatedSchedulerFactory"/>): at boot the host fails to
/// start, and after a deferred start the refusal is logged as Critical and every enqueue on the node fails.
/// </para>
/// </remarks>
internal sealed class JobsSchedulerHostedService(
    IServiceProvider services,
    JobStoreDatabase database,
    JobHandlerRegistry registry,
    IOptions<BackgroundJobsOptions> options,
    JobsShutdownSignal shutdownSignal,
    ILogger<JobsSchedulerHostedService> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan UnreachableRetryDelay = TimeSpan.FromSeconds(5);

    // A lost race clears as soon as the other transaction ends, so it is tried again sooner than an outage.
    private static readonly TimeSpan ContendedRetryDelay = TimeSpan.FromSeconds(1);

    private readonly CancellationTokenSource _stopping = new();
    private IScheduler? _scheduler;
    private Task? _deferredStart;

    private enum StartResult
    {
        Started,
        Unreachable,
        Contended,
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await TryStartSchedulerAsync(cancellationToken);
        if (result == StartResult.Started)
        {
            return;
        }

        if (result == StartResult.Unreachable)
        {
            logger.LogError(
                "The background jobs scheduler could not reach its database and will start once it can; until then this host runs no jobs.");
        }

        _deferredStart = StartWhenReadyAsync(result, _stopping.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        await (_deferredStart ?? Task.CompletedTask);

        try
        {
            await ShutdownSchedulerAsync(cancellationToken);
        }
        finally
        {
            // Only now, with the scheduler no longer listening, are the jobs still running told to stop: they record
            // nothing, and the firing the scheduler abandoned is what runs them again. Told even when the shutdown
            // threw: a handler never told would run on as if its host were still up.
            shutdownSignal.Raise();
        }
    }

    /// <summary>
    /// Cancels first, so a deferred start still waiting for the database ends even when the host is disposed
    /// without having been stopped, for example after another hosted service failed to start.
    /// </summary>
    public void Dispose()
    {
        // Stopping has usually cancelled already; the check also keeps a second Dispose from throwing.
        if (!_stopping.IsCancellationRequested)
        {
            _stopping.Cancel();
        }

        _stopping.Dispose();
    }

    /// <summary>
    /// Running jobs get a bounded time to finish and record their outcome. When it is up the scheduler lets go of
    /// the rest, which leaves them for recovery on the next node to check in.
    /// </summary>
    private async Task ShutdownSchedulerAsync(CancellationToken cancellationToken)
    {
        if (_scheduler is null)
        {
            return;
        }

        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        wait.CancelAfter(TimeSpan.FromSeconds(options.Value.ShutdownWaitSeconds));
        await _scheduler.Shutdown(waitForJobsToComplete: true, wait.Token);
    }

    private async Task StartSchedulerAsync(CancellationToken cancellationToken)
    {
        // The factory checks the snapshot requirement before it builds the scheduler, so a node refused here never
        // acquires a trigger.
        var scheduler = await services
            .GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName)
            .GetScheduler(cancellationToken);

        // Kept before anything else can fail or be cancelled: the factory caches the scheduler it built, so a
        // stop that arrives mid-registration must still find it to shut it down.
        _scheduler = scheduler;

        await StoreDurableJobsAsync(scheduler, cancellationToken);
        if (options.Value.RunInProcess)
        {
            await scheduler.Start(cancellationToken);
        }
    }

    // Every host with a handler keeps its job type's durable job in the store, so a trigger enqueued anywhere has a
    // job to point at.
    private async Task StoreDurableJobsAsync(IScheduler scheduler, CancellationToken cancellationToken)
    {
        foreach (var jobType in registry.JobTypes)
        {
            await scheduler.AddJob(
                QuartzRegistration.DurableJobFor(jobType),
                AddJobOptions.Replacing,
                cancellationToken);
        }
    }

    /// <summary>
    /// Starts the scheduler, or says why not when it is worth trying again: the database is away, before the start
    /// or during it, or it lost a race with another node's transaction, such as two nodes storing their durable jobs
    /// at once. A failure the database refused with is thrown.
    /// </summary>
    private async Task<StartResult> TryStartSchedulerAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await database.IsReachableAsync(cancellationToken))
            {
                return StartResult.Unreachable;
            }

            await StartSchedulerAsync(cancellationToken);
            return StartResult.Started;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && RetryAfter(exception) is { } result)
        {
            return result;
        }
    }

    // Why a failed start is worth trying again, or null when the database refused and waiting would not help.
    private StartResult? RetryAfter(Exception exception)
    {
        if (DatabaseFailures.IsUnreachable(exception))
        {
            return StartResult.Unreachable;
        }

        if (!DatabaseFailures.IsContention(exception))
        {
            return null;
        }

        logger.LogWarning(
            exception,
            "The background jobs scheduler lost a race with another transaction while starting, a deadlock or a lock timeout, and will try again.");
        return StartResult.Contended;
    }

    private async Task StartWhenReadyAsync(StartResult result, CancellationToken stoppingToken)
    {
        try
        {
            await RetryUntilStartedAsync(result, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host stopped before the database came back.
        }
        catch (Exception exception)
        {
            // Startup has already finished, so there is no one left to fail; say it as loudly as possible.
            logger.LogCritical(exception, "The background jobs scheduler could not start; this host runs no jobs.");
        }
    }

    // Waits after each failed try for as long as its kind of failure takes to clear, then tries again.
    private async Task RetryUntilStartedAsync(StartResult result, CancellationToken stoppingToken)
    {
        do
        {
            await Task.Delay(result == StartResult.Contended ? ContendedRetryDelay : UnreachableRetryDelay, stoppingToken);
            result = await TryStartSchedulerAsync(stoppingToken);
        }
        while (result != StartResult.Started);
    }
}
