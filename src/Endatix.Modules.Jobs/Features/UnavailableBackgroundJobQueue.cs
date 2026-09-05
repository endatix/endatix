using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Jobs.Features;

/// <summary>
/// Stands in for the queue on database providers the module does not yet support.
/// </summary>
/// <remarks>
/// Enqueueing writes a row through a provider-specific context, so a queue cannot exist without one.
/// Registering nothing at all would surface that as a dependency-injection failure naming
/// <c>IJobsDbContext</c>, at whichever call site happened to resolve first; this fails in the
/// same place with a message that names the actual constraint.
/// </remarks>
internal sealed class UnavailableBackgroundJobQueue : IBackgroundJobQueue
{
    private const string Message =
        "Background jobs require PostgreSQL. Set the connection string setting " +
        "'DefaultConnection_DbProvider' to 'postgresql' to enable them.";

    public Task<long> EnqueueAsync(
        BackgroundJobRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Message);

    public Task<IReadOnlyList<long>> EnqueueManyAsync(
        IReadOnlyList<BackgroundJobRequest> requests,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Message);
}
