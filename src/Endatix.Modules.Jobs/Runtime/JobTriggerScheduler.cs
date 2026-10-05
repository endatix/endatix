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
    public async Task ScheduleAndCommitAsync(
        IDbContextTransaction transaction,
        IReadOnlyList<BackgroundJob> jobs,
        CancellationToken cancellationToken)
    {
        // Built before enlisting: the scheduler refuses to be started for the first time inside an enlistment.
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
