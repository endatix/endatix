using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Routes a job type to the handler registered for it.
/// </summary>
/// <remarks>
/// Handlers are resolved from a scope and never held, because a handler may depend on scoped services such as a
/// <c>DbContext</c>. Job types are compared ordinally: a job type is a routing key written by the enqueuing code,
/// not text a person types.
/// </remarks>
internal sealed class BackgroundJobHandlerRegistry(IServiceScopeFactory scopeFactory)
{
    // The job type is stored in a varchar(128) column, so a longer one could never be routed back to its handler.
    private const int JobTypeMaxLength = 128;

    private IReadOnlyCollection<string>? _jobTypes;

    /// <summary>
    /// The job types this instance can run. A job of any other type is left for an instance that has its handler,
    /// so it keeps its status here and consumes no attempt.
    /// </summary>
    /// <remarks>
    /// Read once and kept: DI registrations do not change while the host runs, and two first reads at once cost one
    /// extra resolution of the handlers and nothing more.
    /// </remarks>
    public IReadOnlyCollection<string> JobTypes => _jobTypes ??= ReadJobTypes();

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> naming the offending handler when its job type is blank,
    /// longer than the column allows, or shared with another handler, which would leave routing ambiguous.
    /// </summary>
    public void Validate() => _ = JobTypes;

    /// <summary>
    /// The handler <paramref name="jobType"/> routes to, from the scope the job will run in.
    /// </summary>
    public IBackgroundJobHandler Resolve(IServiceProvider handlerScope, string jobType) =>
        handlerScope.GetServices<IBackgroundJobHandler>()
            .FirstOrDefault(handler => string.Equals(handler.JobType, jobType, StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"No background job handler is registered for job type '{jobType}'.");

    private IReadOnlyCollection<string> ReadJobTypes()
    {
        using var scope = scopeFactory.CreateScope();
        HashSet<string> jobTypes = new(StringComparer.Ordinal);

        foreach (var handler in scope.ServiceProvider.GetServices<IBackgroundJobHandler>())
        {
            var jobType = handler.JobType;
            var handlerName = handler.GetType().FullName;

            if (string.IsNullOrWhiteSpace(jobType))
            {
                throw new InvalidOperationException(
                    $"The background job handler {handlerName} declares no job type, so nothing can be routed to it.");
            }

            if (jobType.Length > JobTypeMaxLength)
            {
                throw new InvalidOperationException(
                    $"The job type '{jobType}' of the background job handler {handlerName} is longer than " +
                    $"{JobTypeMaxLength} characters, so no job could ever carry it.");
            }

            if (!jobTypes.Add(jobType))
            {
                throw new InvalidOperationException(
                    $"More than one background job handler is registered for the job type '{jobType}'. " +
                    $"{handlerName} is one of them.");
            }
        }

        return jobTypes;
    }
}
