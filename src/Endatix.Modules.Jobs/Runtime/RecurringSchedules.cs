using System.Security.Cryptography;
using System.Text;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

internal sealed class RecurringSchedules(IEnumerable<RecurringJobSchedule> declared, IOptions<BackgroundJobsOptions> options)
{
    private sealed record Schedule(IJobDetail Job, string Group, CronExpression Cron)
    {
        internal ITrigger Trigger(TimeProvider clock) => TriggerBuilder.Create(clock)
            .WithIdentity(Job.Key.Name, Job.Key.Group).ForJob(Job.Key).WithExecutionGroup(Group)
            .WithSchedule(CronScheduleBuilder.Create(Cron)).Build();
    }

    private readonly Schedule[] _schedules = Build(declared, options.Value.Retention.Cron);

    internal IEnumerable<string> Groups => _schedules.Select(schedule => schedule.Group);

    private static Schedule[] Build(IEnumerable<RecurringJobSchedule> declared, string retentionCron)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var schedules = declared.Select(schedule => ForHandler(schedule, names)).ToList();
        schedules.Add(new(DurableJob<JobRetentionJob>(JobRetentionJob.Group), JobRetentionJob.Group, new CronExpression(retentionCron)));
        return [.. schedules];
    }

    private static Schedule ForHandler(RecurringJobSchedule schedule, HashSet<string> names)
    {
        if (!names.Add(schedule.Name))
        {
            throw new InvalidOperationException($"Duplicate recurring schedule name '{schedule.Name}'.");
        }
        var job = DurableJob<RecurringJobExecution>("recurring." + schedule.Name);
        // Execution groups share a database namespace with queue types (at most 128 characters).
        // A key longer than that cannot claim an existing handler's slots, including on older workers.
        var group = "recurring." + Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(job.Key.Name)));
        return new(job, group, new CronExpression(schedule.Cron, TimeZoneInfo.Utc));
    }

    private static IJobDetail DurableJob<T>(string name) where T : class, IJob =>
        JobBuilder.Create<T>().WithIdentity(name, QuartzRegistration.MaintenanceJobGroup).StoreDurably().Build();

    internal async Task StoreAsync(IScheduler scheduler, TimeProvider clock, CancellationToken ct)
    {
        foreach (var schedule in _schedules)
        {
            await scheduler.AddJob(schedule.Job, AddJobOptions.Replacing, ct);
            await StoreTriggerAsync(scheduler, (ICronTrigger)schedule.Trigger(clock), ct);
        }
    }

    private static async Task StoreTriggerAsync(IScheduler scheduler, ICronTrigger trigger, CancellationToken ct)
    {
        var existing = await scheduler.GetTrigger(trigger.Key, ct);
        if (existing is ICronTrigger cron && cron.CronExpressionString == trigger.CronExpressionString
            && cron.TimeZone.Id == trigger.TimeZone.Id && cron.ExecutionGroup == trigger.ExecutionGroup)
        {
            return;
        }
        await scheduler.ScheduleTrigger(trigger, existing is null ? TriggerConflict.Keep : TriggerConflict.Replace, ct);
    }

    [DisallowConcurrentExecution]
    private sealed class RecurringJobExecution(IServiceProvider scope, JobsShutdownSignal shutdown) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, shutdown.Token);
            var name = context.JobDetail.Key.Name["recurring.".Length..];
            await scope.GetRequiredKeyedService<IRecurringJob>(name).RunAsync(stopping.Token);
        }
    }
}
