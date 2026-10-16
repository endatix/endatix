using System.Reflection;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Extensibility;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// What a node's scheduler was built with, read from the scheduler Quartz built rather than from the options it was
/// built from: its thread pool, how many triggers one acquisition takes, its store's dialect and its trigger listeners.
/// </summary>
/// <remarks>Quartz exposes none of the first three, so they are read by reflection.</remarks>
internal sealed record SchedulerShape(int PoolSize, int MaxBatchSize, string DriverDelegate, string TriggerListeners)
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
            (int)Member(Member(resources, "ThreadPool"), "PoolSize"),
            (int)Member(resources, "MaxBatchSize"),
            DriverDelegateOf((IJobStore)Member(resources, "JobStore")).GetType().Name,
            string.Join(",", listeners));
    }

    // The store may be wrapped, for tracing say; the dialect is on the ADO store underneath.
    private static object DriverDelegateOf(IJobStore store)
    {
        for (object current = store; ;)
        {
            if (current.GetType().GetProperty("Delegate", Instance) is { } dialect)
            {
                return dialect.GetValue(current)!;
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
