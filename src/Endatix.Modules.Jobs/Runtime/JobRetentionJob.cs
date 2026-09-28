using Endatix.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Deletes finished job rows whose retention has passed, on a cron schedule and on one node at a time.
/// </summary>
/// <remarks>
/// Retention is not optional housekeeping: fan-out writes a row per subscriber per event, so without it the table
/// grows without bound. The scheduler deletes its own finished triggers, so only job rows need collecting. A row
/// that is not finished is never deleted, whatever its expiry says.
/// </remarks>
[DisallowConcurrentExecution]
internal sealed class JobRetentionJob(
    IServiceScopeFactory scopeFactory,
    IDateTimeProvider dateTimeProvider,
    IOptions<BackgroundJobsOptions> options,
    ILogger<JobRetentionJob> logger) : IJob
{
    /// <summary>The job's execution group and name, beside the job types' own.</summary>
    public const string Group = "JobRetention";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        await RunAsync(cancellationToken);

    /// <summary>Deletes expired finished rows in bounded batches and returns how many it deleted.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var retention = options.Value.Retention;
        var utcNow = dateTimeProvider.UtcNow.UtcDateTime;
        var deleted = 0;

        for (var batch = 0; batch < retention.MaxBatchesPerRun; batch++)
        {
            // A scope per batch, so the context never holds more than one statement's worth of work.
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBackgroundJobStateRepository>();
            var deletedInBatch = await repository.DeleteExpiredAsync(utcNow, retention.BatchSize, cancellationToken);
            deleted += deletedInBatch;

            if (deletedInBatch < retention.BatchSize)
            {
                break;
            }
        }

        logger.LogInformation("Deleted {Count} expired background job rows", deleted);
        return deleted;
    }
}
