using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Schedules the trigger of each newly inserted job row inside the transaction that inserted it.
/// </summary>
internal interface IJobTriggerScheduler
{
    /// <summary>
    /// Makes sure this node may store triggers and that its scheduler is built, before the caller opens the
    /// transaction the triggers join, so neither holds a second connection while that transaction is open.
    /// </summary>
    /// <exception cref="InvalidOperationException">This node may not store triggers.</exception>
    Task PrepareAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Schedules one trigger per job in <paramref name="transaction"/>, then commits it, so the rows and their
    /// triggers become visible together or not at all.
    /// </summary>
    Task ScheduleAndCommitAsync(
        IDbContextTransaction transaction,
        IReadOnlyList<BackgroundJob> jobs,
        CancellationToken cancellationToken);
}

/// <inheritdoc cref="IJobTriggerScheduler" />
internal sealed class QuartzJobTriggerScheduler(
    [FromKeyedServices(QuartzRegistration.SchedulerName)] ISchedulerFactory schedulerFactory,
    StoredDurableJobs durableJobs) : IJobTriggerScheduler
{
    // The factory refuses a node that may not store triggers lock-free (ReadCommittedSnapshotGatedSchedulerFactory),
    // whether its start was refused or is still deferred. The scheduler is built here, outside any enlistment: it
    // refuses to be started for the first time inside one.
    public async Task PrepareAsync(CancellationToken cancellationToken) =>
        await schedulerFactory.GetScheduler(cancellationToken);

    public async Task ScheduleAndCommitAsync(
        IDbContextTransaction transaction,
        IReadOnlyList<BackgroundJob> jobs,
        CancellationToken cancellationToken)
    {
        // Already built by PrepareAsync, so this returns the factory's cached scheduler.
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobTypes = jobs.Select(job => job.JobType).Distinct(StringComparer.Ordinal).ToList();

        // The enlistment flows with this async context only, so it is opened here, around both the scheduling
        // and the commit, and closed after the commit, when the scheduler is told about triggers it can see.
        using (scheduler.EnlistTransaction(transaction.GetDbTransaction()))
        {
            await durableJobs.EnsureAsync(scheduler, jobTypes, cancellationToken);
            await ScheduleTriggersAsync(scheduler, jobs, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        durableJobs.Remember(jobTypes);
    }

    private async Task ScheduleTriggersAsync(
        IScheduler scheduler,
        IReadOnlyList<BackgroundJob> jobs,
        CancellationToken cancellationToken)
    {
        foreach (var job in jobs)
        {
            await durableJobs.ScheduleAsync(
                scheduler,
                QuartzRegistration.TriggerFor(new JobTriggerSpec(job.Id, job.JobType)),
                cancellationToken);
        }
    }
}
