using Endatix.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
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
/// cannot fix a database.
/// </para>
/// </remarks>
internal sealed class JobsSchedulerHostedService(
    IServiceProvider services,
    IConfiguration configuration,
    JobHandlerRegistry registry,
    IOptions<BackgroundJobsOptions> options,
    ILogger<JobsSchedulerHostedService> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan UnreachableRetryDelay = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _stopping = new();
    private IScheduler? _scheduler;
    private Task? _deferredStart;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await IsDatabaseReachableAsync(cancellationToken))
        {
            await StartSchedulerAsync(cancellationToken);
            return;
        }

        logger.LogError(
            "The background jobs scheduler could not reach its database and will start once it can; until then this host runs no jobs.");
        _deferredStart = StartWhenReachableAsync(_stopping.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();

        if (_deferredStart is not null)
        {
            await _deferredStart;
        }

        if (_scheduler is not null)
        {
            await _scheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
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

    private async Task StartSchedulerAsync(CancellationToken cancellationToken)
    {
        var scheduler = await services
            .GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName)
            .GetScheduler(cancellationToken);

        // Every host with a handler keeps its job type's durable job in the store, so a trigger enqueued anywhere
        // has a job to point at.
        foreach (var jobType in registry.JobTypes)
        {
            await scheduler.AddJob(
                QuartzRegistration.DurableJobFor(jobType),
                AddJobOptions.Replacing,
                cancellationToken);
        }

        _scheduler = scheduler;

        if (options.Value.RunInProcess)
        {
            await scheduler.Start(cancellationToken);
        }
    }

    private async Task StartWhenReachableAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!await IsDatabaseReachableAsync(stoppingToken))
            {
                await Task.Delay(UnreachableRetryDelay, stoppingToken);
            }

            await StartSchedulerAsync(stoppingToken);
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

    private async Task<bool> IsDatabaseReachableAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection =
                new NpgsqlConnection(ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration));
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            return false;
        }
    }
}
