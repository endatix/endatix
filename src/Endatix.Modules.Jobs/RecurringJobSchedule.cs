using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.Modules.Jobs;

/// <summary>Contributes UTC cron schedules to the Jobs module before the host finalizes its modules.</summary>
public static class RecurringJobServiceCollectionExtensions
{
    /// <summary>Registers a schedule by its stable, cluster-wide name. With Jobs disabled the declaration is inert.</summary>
    public static IServiceCollection AddRecurringJob<T>(this IServiceCollection services, string name, string cron)
        where T : class, IRecurringJob
    {
        var schedule = RecurringJobSchedule.Create<T>(name, cron);
        var existing = RecurringJobSchedule.Declared(services).SingleOrDefault(other =>
            StringComparer.OrdinalIgnoreCase.Equals(other.Group, schedule.Group));
        if (existing is null)
        {
            services.AddSingleton(schedule);
        }
        else if (existing.Group != schedule.Group || existing.Handler != typeof(T) || existing.Cron != cron)
        {
            throw new ArgumentException("Recurring job names must be unique ignoring case.", nameof(name));
        }
        return services;
    }
}

internal sealed record RecurringJobSchedule(string Group, Type Handler, string Cron)
{
    internal const string Prefix = "recurring.";

    internal static IEnumerable<RecurringJobSchedule> Declared(IServiceCollection services) =>
        services.Where(service => service.ServiceType == typeof(RecurringJobSchedule))
            .Select(service => (RecurringJobSchedule)service.ImplementationInstance!);

    internal static RecurringJobSchedule Create<T>(string name, string cron) where T : class, IRecurringJob
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 100 || name != name.Trim() || !CronExpression.TryParse(cron, out _))
        {
            throw new ArgumentException("Provide a trimmed name of up to 100 characters and a valid cron expression.");
        }
        return new(Prefix + name, typeof(T), cron);
    }

    internal static void RegisterAll(IQuartzBuilder quartz, string? retentionCron, RecurringJobSchedule[] schedules)
    {
        // Leave an invalid retention cron to the options validator, which names the configuration key.
        var retention = CronExpression.TryParse(retentionCron ?? "", out var parsed)
            ? CronScheduleBuilder.Create(parsed) : null;
        RegisterJob<JobRetentionJob>(quartz, JobRetentionJob.Group, retention);
        foreach (var schedule in schedules)
        {
            quartz.Services.AddKeyedScoped(typeof(IRecurringJob), schedule.Group, schedule.Handler);
            var cron = CronScheduleBuilder.Create(schedule.Cron).InTimeZone(TimeZoneInfo.Utc);
            RegisterJob<Execution>(quartz, schedule.Group, cron);
        }
    }

    private static void RegisterJob<T>(IQuartzBuilder quartz, string group, CronScheduleBuilder? schedule)
        where T : class, IJob
    {
        var key = new JobKey(group, QuartzRegistration.MaintenanceJobGroup);
        quartz.AddJob<T>(job => job.WithIdentity(key).StoreDurably());
        if (schedule is not null)
        {
            quartz.AddTrigger(trigger => trigger.WithIdentity(group, key.Group).ForJob(key)
                .WithExecutionGroup(group).WithSchedule(schedule));
        }
    }

    [DisallowConcurrentExecution]
    private sealed class Execution(IServiceProvider scope, JobsShutdownSignal shutdown) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, shutdown.Token);
            var job = scope.GetRequiredKeyedService<IRecurringJob>(context.JobDetail.Key.Name);
            await job.RunAsync(stopping.Token);
        }
    }
}
