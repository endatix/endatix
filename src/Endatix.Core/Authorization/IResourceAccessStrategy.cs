using Endatix.Core.Infrastructure.Caching;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Authorization;

/// <summary>
/// Defines a contract for computing contextual permissions for a specific resource scenario.
/// </summary>
/// <typeparam name="TAccessData">The DTO containing the computed permissions (e.g. FormAccessData)</typeparam>
/// <typeparam name="TAccessContext">The input context required to make the decision (e.g. SubmissionAccessContext)</typeparam>
public interface IResourceAccessStrategy<TAccessData, in TAccessContext>
    where TAccessData : class
    where TAccessContext : class
{
    /// <summary>
    /// Gets the cached access data for the given context wrapped in an envelope.
    /// </summary>
    /// <param name="context">The context to get the access data for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cached access data for the given context.</returns>
    Task<Result<Cached<TAccessData>>> GetAccessData(TAccessContext context, CancellationToken cancellationToken);
}
