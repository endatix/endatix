using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The job types this host can run, and the handler each one routes to.
/// </summary>
/// <remarks>
/// <para>
/// Built once at startup from every <see cref="IBackgroundJobHandler"/> in DI, so handlers contributed by
/// assemblies this module does not reference register with no change here. A job type with no handler on this
/// host is not an error: another host that has the handler runs it.
/// </para>
/// <para>
/// Handlers are resolved from the job's own scope and never held, because a handler may depend on scoped
/// services such as a <c>DbContext</c>. A handler registered keyed by its job type is the only one a job run
/// builds; one registered without a key is found by building every handler. Job types are compared ordinally: a
/// job type is a routing key written by code, not text a person types. They must still be unique ignoring case,
/// because a job store on SQL Server keys each job type's durable scheduler job under the database collation, which
/// ignores case by default, so two job types differing only in case would share one durable job.
/// </para>
/// </remarks>
internal sealed class JobHandlerRegistry
{
    // The job type is stored in a varchar(128) column and is a scheduler job key, so a longer one could never be
    // routed back to its handler.
    private const int JobTypeMaxLength = 128;

    private readonly Dictionary<string, Type> _handlerTypes;

    private JobHandlerRegistry(Dictionary<string, Type> handlerTypes) => _handlerTypes = handlerTypes;

    /// <summary>The job types this host has a handler for.</summary>
    public IReadOnlyCollection<string> JobTypes => _handlerTypes.Keys;

    /// <summary>Whether this host has a handler for <paramref name="jobType"/>.</summary>
    public bool Contains(string jobType) => _handlerTypes.ContainsKey(jobType);

    /// <summary>
    /// Builds the registry. Throws <see cref="InvalidOperationException"/> naming the job type when a handler
    /// declares a blank, reserved or over-long one, or when two handlers declare the same one, which would leave routing
    /// ambiguous, or two that differ only in case.
    /// </summary>
    public static JobHandlerRegistry Build(IEnumerable<IBackgroundJobHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        // Collected ignoring case, so job types that differ only in case are refused, and then routed ordinally.
        Dictionary<string, HandlerEntry> handlerTypes = new(StringComparer.OrdinalIgnoreCase);

        foreach (var handler in handlers)
        {
            AddHandler(handlerTypes, handler);
        }

        return new JobHandlerRegistry(handlerTypes.Values.ToDictionary(
            entry => entry.JobType, entry => entry.HandlerType, StringComparer.Ordinal));
    }

    /// <summary>
    /// Builds the registry from the handlers registered in <paramref name="services"/>, in a scope of its own
    /// because handlers may be scoped.
    /// </summary>
    public static JobHandlerRegistry Build(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var registry = Build(scope.ServiceProvider.GetServices<IBackgroundJobHandler>());

        // A job run trusts the key, so a keyed handler that declares another job type would run the wrong jobs.
        foreach (var jobType in registry.JobTypes)
        {
            if (scope.ServiceProvider.GetKeyedService<IBackgroundJobHandler>(jobType) is { } keyed
                && !string.Equals(keyed.JobType, jobType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The background job handler {keyed.GetType().FullName} is registered for the job type " +
                    $"'{jobType}' but declares '{keyed.JobType}'.");
            }
        }

        return registry;
    }

    private static void AddHandler(Dictionary<string, HandlerEntry> handlerTypes, IBackgroundJobHandler handler)
    {
        var entry = new HandlerEntry(handler.JobType, handler.GetType());
        if (JobTypeError(entry.JobType, entry.HandlerType) is { } error)
        {
            throw new InvalidOperationException(error);
        }

        if (!handlerTypes.TryAdd(entry.JobType, entry))
        {
            throw new InvalidOperationException(DuplicateJobTypeError(handlerTypes[entry.JobType], entry));
        }
    }

    private static string DuplicateJobTypeError(HandlerEntry first, HandlerEntry second)
    {
        if (first.JobType == second.JobType)
        {
            return $"More than one background job handler is registered for the job type '{first.JobType}': " +
                $"{first.HandlerType.FullName} and {second.HandlerType.FullName}.";
        }

        return $"The job types '{first.JobType}' of {first.HandlerType.FullName} and '{second.JobType}' of " +
            $"{second.HandlerType.FullName} differ only in case. Job types must be unique ignoring case, because " +
            "a job store on SQL Server keys their scheduler jobs under a collation that ignores case.";
    }

    /// <summary>Why no job could run under <paramref name="jobType"/>, or <see langword="null"/> when one can.</summary>
    /// <remarks>
    /// A job refuses a type that ends with a space, since SQL Server ignores trailing spaces (U+0020 only) when it
    /// compares; refusing it here names the handler at startup rather than failing every enqueue of it.
    /// </remarks>
    private static string? JobTypeError(string? jobType, Type handlerType) => jobType switch
    {
        _ when string.IsNullOrWhiteSpace(jobType) =>
            $"The background job handler {handlerType.FullName} declares no job type, so nothing can be routed to it.",

        // Every job type is also an execution group, and the scheduler reserves these names for its own limits and
        // its maintenance jobs; a handler declaring one would fail building the scheduler, or share the maintenance
        // jobs' slots, so either could starve the other. Compared ignoring case, as job types are with each other.
        _ when IsReservedExecutionGroupName(jobType) =>
            $"The job type '{jobType}' of the background job handler {handlerType.FullName} is, ignoring case, a " +
            $"name the scheduler reserves for its execution limits or for its maintenance jobs ('{JobRetentionJob.Group}'), " +
            "so no job could safely run under it.",
        { } type when type[^1] == ' ' =>
            $"The job type '{jobType}' of the background job handler {handlerType.FullName} ends with a space.",
        { Length: > JobTypeMaxLength } =>
            $"The job type '{jobType}' of the background job handler {handlerType.FullName} is longer than " +
            $"{JobTypeMaxLength} characters, so no job could ever carry it.",
        _ => null,
    };

    private static bool IsReservedExecutionGroupName(string jobType) =>
        jobType.Trim() is "*" or "_" ||
        jobType.Trim().Equals("null", StringComparison.OrdinalIgnoreCase) ||
        jobType.Trim().Equals(JobRetentionJob.Group, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The handler <paramref name="jobType"/> routes to, from the scope the job runs in, or <see langword="null"/>
    /// when this host has none.
    /// </summary>
    public IBackgroundJobHandler? Resolve(IServiceProvider jobScope, string jobType)
    {
        if (!_handlerTypes.ContainsKey(jobType))
        {
            return null;
        }

        return jobScope.GetKeyedService<IBackgroundJobHandler>(jobType)
            ?? jobScope.GetServices<IBackgroundJobHandler>().FirstOrDefault(handler =>
                string.Equals(handler.JobType, jobType, StringComparison.Ordinal));
    }

    /// <param name="JobType">The job type as the handler declares it.</param>
    /// <param name="HandlerType">The handler's type.</param>
    private readonly record struct HandlerEntry(string JobType, Type HandlerType);
}
