using System.Globalization;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// The job triggers whose job rows are gone. The test reset empties the jobs table but keeps the scheduler's own
/// tables, so a job a test enqueued and never ran leaves its trigger for whichever test starts the scheduler next.
/// </summary>
/// <remarks>
/// Only job triggers are judged: the retention schedule and any other trigger outside the job group stay. A job
/// enqueued before the scheduler starts has its row, so its trigger stays too.
/// </remarks>
internal static class StaleJobTriggers
{
    /// <summary>Unschedules every job trigger of <paramref name="scheduler"/> whose job row is gone.</summary>
    /// <returns>How many it unscheduled.</returns>
    public static async Task<int> DropAsync(IScheduler scheduler, IServiceProvider services, CancellationToken cancellationToken)
    {
        var jobIdsByTrigger = await JobIdsByTriggerAsync(scheduler, cancellationToken);
        if (jobIdsByTrigger.Count == 0)
        {
            return 0;
        }

        var existing = await ExistingJobIdsAsync(services, [.. jobIdsByTrigger.Values], cancellationToken);
        TriggerKey[] stale = [.. jobIdsByTrigger.Where(entry => !existing.Contains(entry.Value)).Select(entry => entry.Key)];
        return stale.Length == 0 ? 0 : (await scheduler.UnscheduleJobs(stale, cancellationToken)).Count;
    }

    private static async Task<Dictionary<TriggerKey, long>> JobIdsByTriggerAsync(
        IScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var headers = await scheduler.QueryTriggers(new TriggerQuery { Take = PagedQuery.All }, cancellationToken);
        TriggerKey[] jobTriggers =
            [.. headers.Items.Where(header => header.JobKey.Group == QuartzRegistration.JobGroup).Select(header => header.Key)];
        var triggers = jobTriggers.Length == 0 ? [] : await scheduler.GetTriggers(jobTriggers, cancellationToken);
        return triggers
            .Select(trigger => (trigger.Key, JobId: JobIdOf(trigger)))
            .Where(entry => entry.JobId is not null)
            .ToDictionary(entry => entry.Key, entry => entry.JobId!.Value);
    }

    private static long? JobIdOf(ITrigger trigger) =>
        trigger.JobDataMap.TryGetString(BackgroundJobExecution.JobIdKey, out var text)
            && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var jobId)
            ? jobId
            : null;

    private static async Task<HashSet<long>> ExistingJobIdsAsync(
        IServiceProvider services,
        long[] jobIds,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var existing = await scope.ServiceProvider.GetRequiredService<IJobsDbContext>().BackgroundJobs
            .IgnoreQueryFilters()
            .Where(job => jobIds.Contains(job.Id))
            .Select(job => job.Id)
            .ToListAsync(cancellationToken);
        return [.. existing];
    }
}
