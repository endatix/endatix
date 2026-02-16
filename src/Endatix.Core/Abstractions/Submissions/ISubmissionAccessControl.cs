using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Service for computing contextual submission authorization by composing RBAC with resource-specific scopes.
/// This follows the "Cached Identity, Dynamic Scopes" pattern: Identity (RBAC) is stable and cacheable;
/// Resource Access (ReBAC) is volatile and contextual.
/// </summary>
public interface ISubmissionAccessControl
{
    /// <summary>
    /// Gets contextual authorization data by fetching cached RBAC identity and composing it with resource-specific scopes.
    /// Checks in order: Admin -> Access Token -> Authenticated User -> Public Form
    /// </summary>
    /// <param name="context">The access context containing form/submission/token info</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing ResourceAccessData with Identity and Scopes</returns>
    Task<Result<ResourceAccessData>> GetAccessDataAsync(
        SubmissionAccessContext context,
        CancellationToken cancellationToken);
}
