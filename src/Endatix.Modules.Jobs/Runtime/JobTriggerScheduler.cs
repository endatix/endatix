using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    IOptions<BackgroundJobsOptions> options) : IJobTriggerScheduler
{
    public async Task ScheduleAndCommitAsync(
        IDbContextTransaction transaction,
        IReadOnlyList<BackgroundJob> jobs,
        CancellationToken cancellationToken)
    {
        // Built before enlisting: the scheduler refuses to be started for the first time inside an enlistment.
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);

        // The enlistment flows with this async context only, so it is opened here, around both the scheduling
        // and the commit, and closed after the commit, when the scheduler is told about triggers it can see.
        using (scheduler.EnlistTransaction(transaction.GetDbTransaction()))
        {
            await EnsureDurableJobsAsync(scheduler, jobs, cancellationToken);
            await ScheduleTriggersAsync(scheduler, jobs, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    // A job type is enqueued from any host, including one without its handler, so the durable job a trigger
    // points at may not have been stored yet by a host that has one.
    private static async Task EnsureDurableJobsAsync(
        IScheduler scheduler,
        IReadOnlyList<BackgroundJob> jobs,
        CancellationToken cancellationToken)
    {
        foreach (var jobType in jobs.Select(job => job.JobType).Distinct(StringComparer.Ordinal))
        {
            if (!await scheduler.Exists(QuartzRegistration.JobKeyFor(jobType), cancellationToken))
            {
                await scheduler.AddJob(QuartzRegistration.DurableJobFor(jobType), AddJobOptions.Replacing, cancellationToken);
            }
        }
    }

    private async Task ScheduleTriggersAsync(
        IScheduler scheduler,
        IReadOnlyList<BackgroundJob> jobs,
        CancellationToken cancellationToken)
    {
        foreach (var job in jobs)
        {
            var policy = options.Value.ResolvePolicy(job.JobType);
            await scheduler.ScheduleJob(
                QuartzRegistration.TriggerFor(new JobTriggerSpec(job.Id, job.JobType, policy)),
                cancellationToken: cancellationToken);
        }
    }
}
