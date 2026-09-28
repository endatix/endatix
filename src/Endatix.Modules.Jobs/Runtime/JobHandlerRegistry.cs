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
/// services such as a <c>DbContext</c>. Job types are compared ordinally: a job type is a routing key written by
/// code, not text a person types.
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
    /// declares a blank or over-long one, or when two handlers declare the same one, which would leave routing
    /// ambiguous.
    /// </summary>
    public static JobHandlerRegistry Build(IEnumerable<IBackgroundJobHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        Dictionary<string, Type> handlerTypes = new(StringComparer.Ordinal);

        foreach (var handler in handlers)
        {
            var jobType = handler.JobType;
            var handlerType = handler.GetType();

            if (string.IsNullOrWhiteSpace(jobType))
            {
                throw new InvalidOperationException(
                    $"The background job handler {handlerType.FullName} declares no job type, so nothing can be routed to it.");
            }

            if (jobType.Length > JobTypeMaxLength)
            {
                throw new InvalidOperationException(
                    $"The job type '{jobType}' of the background job handler {handlerType.FullName} is longer than " +
                    $"{JobTypeMaxLength} characters, so no job could ever carry it.");
            }

            if (!handlerTypes.TryAdd(jobType, handlerType))
            {
                throw new InvalidOperationException(
                    $"More than one background job handler is registered for the job type '{jobType}': " +
                    $"{handlerTypes[jobType].FullName} and {handlerType.FullName}.");
            }
        }

        return new JobHandlerRegistry(handlerTypes);
    }

    /// <summary>
    /// Builds the registry from the handlers registered in <paramref name="services"/>, in a scope of its own
    /// because handlers may be scoped.
    /// </summary>
    public static JobHandlerRegistry Build(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return Build(scope.ServiceProvider.GetServices<IBackgroundJobHandler>());
    }

    /// <summary>
    /// The handler <paramref name="jobType"/> routes to, from the scope the job runs in, or <see langword="null"/>
    /// when this host has none.
    /// </summary>
    public IBackgroundJobHandler? Resolve(IServiceProvider jobScope, string jobType)
    {
        if (!_handlerTypes.TryGetValue(jobType, out var handlerType))
        {
            return null;
        }

        return jobScope.GetServices<IBackgroundJobHandler>().FirstOrDefault(handler => handler.GetType() == handlerType);
    }
}
