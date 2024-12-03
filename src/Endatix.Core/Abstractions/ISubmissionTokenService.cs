using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Represents a service that manages secure tokens for submission identifiers.
/// This service provides functionality to obtain tokens for submissions
/// and resolve those tokens back to submission identifiers.
/// </summary>
public interface ISubmissionTokenService
{
    /// <summary>
    /// Obtains a secure token for the specified submission identifier.
    /// The token can be safely exposed in public contexts (like URLs or client-side storage).
    /// </summary>
    /// <param name="submissionId">The submission identifier to get a token for</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A Result containing the secure token string if successful</returns>
    Task<Result<string>> ObtainTokenAsync(long submissionId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a secure token back to its original submission identifier.
    /// </summary>
    /// <param name="token">The token to resolve</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A Result containing the submission identifier if successful</returns>
    Task<Result<long>> ResolveTokenAsync(string token, CancellationToken cancellationToken);
}
