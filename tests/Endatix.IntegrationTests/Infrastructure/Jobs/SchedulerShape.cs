using System.Reflection;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Extensibility;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// What a node's scheduler was built with, read from the scheduler Quartz built rather than from the options it was
/// built from.
/// </summary>
/// <remarks>Quartz exposes the execution limits, the jobs and the triggers; the rest is read by reflection.</remarks>
/// <param name="Pool">The thread pool's size and how many triggers one acquisition takes.</param>
/// <param name="Store">The store's dialect and settings.</param>
/// <param name="Timing">How long the scheduler idles, and how far ahead a batch reaches.</param>
/// <param name="TriggerListeners">The trigger listeners, by type.</param>
/// <param name="ExecutionLimits">Each execution group's cap.</param>
/// <param name="Retention">The retention job and its trigger, as the scheduler reads them from its store.</param>
internal sealed record SchedulerShape(
    string Pool,
    string Store,
    string Timing,
    string TriggerListeners,
    string ExecutionLimits,
    string Retention)
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static async Task<SchedulerShape> ReadAsync(JobsTestNode node, CancellationToken ct)
    {
        var scheduler = await node.Services
            .GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName)
            .GetScheduler(ct);
        var resources = Member(Member(scheduler, "scheduler"), "resources");
        var listeners = scheduler.ListenerManager.GetTriggerListeners().Select(listener => listener.GetType().Name).Order(StringComparer.Ordinal);
        return new SchedulerShape(
            $"threads={Member(Member(resources, "ThreadPool"), "PoolSize")} batch={Member(resources, "MaxBatchSize")}",
            StoreOf(AdoStoreOf((IJobStore)Member(resources, "JobStore"))),
            $"idle={Member(resources, "IdleWaitTime")} batchWindow={Member(resources, "BatchTimeWindow")}",
            string.Join(",", listeners),
            LimitsOf(await scheduler.GetExecutionLimits(ct)),
            await RetentionOf(scheduler, ct));
    }

    private static string StoreOf(object store) =>
        $"dialect={Member(store, "Delegate").GetType().Name} lockOnInsert={Member(store, "LockOnInsert")} " +
        $"clustered={Member(store, "Clustered")} checkinMisfire={Member(store, "ClusterCheckinMisfireThreshold")} " +
        $"misfire={Member(store, "MisfireThreshold")} misfirePass={Member(store, "MisfireHandlerFrequency")}";

    private static string LimitsOf(ExecutionLimits? limits) =>
        string.Join(";", (limits?.Groups ?? []).Select(group => string.Join(
            ",",
            group.GetType().GetProperties().Select(property => $"{property.Name}={property.GetValue(group)}"))).Order(StringComparer.Ordinal));

    private static async Task<string> RetentionOf(IScheduler scheduler, CancellationToken ct)
    {
        var job = await scheduler.GetJobDetail(new JobKey(JobRetentionJob.Group, QuartzRegistration.MaintenanceJobGroup), ct);
        var trigger = await scheduler.GetTrigger(new TriggerKey(JobRetentionJob.Group, QuartzRegistration.MaintenanceJobGroup), ct);
        return $"job={job?.JobType} durable={job?.Durable} trigger={(trigger as ICronTrigger)?.CronExpressionString}";
    }

    // The store may be wrapped, for tracing say; the settings are on the ADO store underneath.
    private static object AdoStoreOf(IJobStore store)
    {
        for (object current = store; ;)
        {
            if (current.GetType().GetProperty("Delegate", Instance) is not null)
            {
                return current;
            }

            current = FieldsOf(current.GetType()).Select(field => field.GetValue(current)).OfType<IJobStore>().FirstOrDefault()
                ?? throw new InvalidOperationException($"No ADO job store under {store.GetType().Name}.");
        }
    }

    private static IEnumerable<FieldInfo> FieldsOf(Type? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(Instance | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    private static object Member(object target, string name)
    {
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetField(name, Instance | BindingFlags.DeclaredOnly) is { } field)
            {
                return field.GetValue(target)!;
            }

            if (type.GetProperty(name, Instance | BindingFlags.DeclaredOnly) is { } property)
            {
                return property.GetValue(target)!;
            }
        }

        throw new InvalidOperationException($"{target.GetType().Name} has no member '{name}'.");
    }
}
