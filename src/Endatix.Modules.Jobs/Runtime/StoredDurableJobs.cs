using System.Collections.Concurrent;
using System.Data.Common;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The job types whose durable scheduler job this process has seen in the store, so that enqueueing asks the store
/// about each job type once per process rather than once per enqueue.
/// </summary>
/// <remarks>
/// A job remembered here can still disappear from the store behind the process's back, deleted by an operator or
/// with its database. Scheduling a trigger for it then fails, and <see cref="ScheduleAsync"/> stores the job again
/// and schedules the trigger once more.
/// </remarks>
internal sealed class StoredDurableJobs
{
    private readonly ConcurrentDictionary<string, bool> _jobTypes = new(StringComparer.Ordinal);

    /// <summary>
    /// Stores the durable job of each of <paramref name="jobTypes"/> that this process has not yet seen in the store
    /// and that is not there.
    /// </summary>
    /// <remarks>
    /// A job type is enqueued from any host, including one without its handler, so the durable job a trigger points
    /// at may not have been stored yet by a host that has one.
    /// </remarks>
    public async Task EnsureAsync(IScheduler scheduler, IEnumerable<string> jobTypes, CancellationToken cancellationToken)
    {
        foreach (var jobType in jobTypes.Where(jobType => !_jobTypes.ContainsKey(jobType)))
        {
            if (!await scheduler.Exists(QuartzRegistration.JobKeyFor(jobType), cancellationToken))
            {
                await StoreAsync(scheduler, jobType, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Remembers <paramref name="jobTypes"/> as stored. Called only once the transaction that found or stored their
    /// jobs has committed, because a job stored by a transaction that rolled back is not in the store.
    /// </summary>
    public void Remember(IEnumerable<string> jobTypes)
    {
        foreach (var jobType in jobTypes)
        {
            _jobTypes.TryAdd(jobType, true);
        }
    }

    /// <summary>
    /// Schedules <paramref name="trigger"/>. When the durable job it points at was remembered but is no longer in the
    /// store, stores the job again and schedules the trigger once more.
    /// </summary>
    public async Task ScheduleAsync(IScheduler scheduler, ITrigger trigger, CancellationToken cancellationToken)
    {
        try
        {
            await scheduler.ScheduleJob(trigger, cancellationToken: cancellationToken);
        }
        // A failure the database reported is not a missing job, and its transaction may no longer take statements.
        catch (JobPersistenceException exception)
            when (exception.InnerException is not DbException && _jobTypes.ContainsKey(trigger.JobKey.Name))
        {
            if (!await StoreAgainIfMissingAsync(scheduler, trigger.JobKey, cancellationToken))
            {
                throw;
            }

            await scheduler.ScheduleJob(trigger, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Stores the durable job <paramref name="jobKey"/> names again when it is no longer in the store, and returns
    /// whether it did. The job type is forgotten too, so the next enqueue looks for it in the store again.
    /// </summary>
    public async Task<bool> StoreAgainIfMissingAsync(IScheduler scheduler, JobKey jobKey, CancellationToken cancellationToken)
    {
        if (await scheduler.Exists(jobKey, cancellationToken))
        {
            return false;
        }

        _jobTypes.TryRemove(jobKey.Name, out _);
        await StoreAsync(scheduler, jobKey.Name, cancellationToken);
        return true;
    }

    private static async Task StoreAsync(IScheduler scheduler, string jobType, CancellationToken cancellationToken) =>
        await scheduler.AddJob(QuartzRegistration.DurableJobFor(jobType), AddJobOptions.Replacing, cancellationToken);
}
