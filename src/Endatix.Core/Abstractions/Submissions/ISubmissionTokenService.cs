using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Submissions;

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
    /// A new token for a submission on <paramref name="form"/>, with the form's expiry or else the tenant's.
    /// Nothing is saved: a create sets it before its insert, so the submission is never stored without one. A
    /// second save could replace a token that a link open, taking the committed submission over, already returned.
    /// </summary>
    Task<Token> NewTokenAsync(Form form, CancellationToken cancellationToken);

    /// <summary>
    /// The response-token lifetime on a form, in hours: the form's own, else its tenant's.
    /// <see langword="null"/> when tokens there never expire.
    /// </summary>
    Task<int?> GetExpiryHoursAsync(long formId, long tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a secure token back to its original submission identifier.
    /// </summary>
    /// <param name="token">The token to resolve</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A Result containing the submission identifier if successful</returns>
    Task<Result<long>> ResolveTokenAsync(string token, CancellationToken cancellationToken);
}
