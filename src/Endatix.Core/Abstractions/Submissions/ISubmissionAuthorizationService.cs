using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Service for computing resource-based submission permissions.
/// These are computed at runtime based on: Admin status, Access Token scope, User RBAC, or Public form settings.
/// </summary>
public interface ISubmissionAuthorizationService
{
    /// <summary>
    /// Computes permissions for a submission based on the authorization context.
    /// Checks in order: Admin -> Access Token -> Authenticated User -> Public Form
    /// </summary>
    /// <param name="formId">The form ID</param>
    /// <param name="submissionId">Optional submission ID for existing submission context</param>
    /// <param name="token">Optional access token for token-based access</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the computed permissions</returns>
    Task<Result<SubmissionPermissionResult>> GetPermissionsAsync(
        long formId,
        long? submissionId,
        string? token,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result containing computed submission permissions with metadata
/// </summary>
public class SubmissionPermissionResult
{
    /// <summary>
    /// The resource ID (form ID or submission ID)
    /// </summary>
    public long ResourceId { get; init; }

    /// <summary>
    /// Resource type (form, submission)
    /// </summary>
    public string ResourceType { get; init; } = "submission";

    /// <summary>
    /// List of computed permission strings
    /// </summary>
    public List<string> Permissions { get; init; } = [];

    /// <summary>
    /// Timestamp when permissions were computed
    /// </summary>
    public DateTime CachedAt { get; init; }

    /// <summary>
    /// When these permissions expire
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// ETag for cache invalidation
    /// </summary>
    public string? ETag { get; init; }
}
