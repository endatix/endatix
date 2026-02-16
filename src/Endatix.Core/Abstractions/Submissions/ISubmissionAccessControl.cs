using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Service for computing contextual submission authorization by composing RBAC with resource-specific scopes.
/// Returns simplified flat permission arrays for O(1) client-side access.
/// Identity (who the user is) should be fetched from /auth/me endpoint.
/// </summary>
public interface ISubmissionAccessControl
{
    /// <summary>
    /// Gets contextual authorization data with flat permission arrays.
    /// Checks in order: Admin -> Access Token -> Authenticated User -> Public Form
    /// </summary>
    /// <param name="context">The access context containing form/submission/token info</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing FormAccessData with flat permission arrays</returns>
    Task<Result<FormAccessData>> GetAccessDataAsync(
        SubmissionAccessContext context,
        CancellationToken cancellationToken);
}
