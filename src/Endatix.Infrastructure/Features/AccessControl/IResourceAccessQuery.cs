using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Defines a read-model query contract for computing contextual permissions for a resource scenario.
/// </summary>
/// <typeparam name="TAccessData">The DTO containing computed permissions.</typeparam>
/// <typeparam name="TAccessContext">The input context required for the decision.</typeparam>
public interface IResourceAccessQuery<TAccessData, in TAccessContext>
    where TAccessData : class
    where TAccessContext : class
{
    /// <summary>
    /// Gets the cached access data for the given context wrapped in an envelope.
    /// </summary>
    Task<Result<Cached<TAccessData>>> GetAccessData(TAccessContext context, CancellationToken cancellationToken);
}
