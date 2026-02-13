namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Response model for form permissions.
/// </summary>
public class GetFormPermissionsResponse
{
    /// <summary>
    /// The resource ID (form ID or submission ID)
    /// </summary>
    public long ResourceId { get; set; }

    /// <summary>
    /// Resource type (form or submission)
    /// </summary>
    public string ResourceType { get; set; } = "submission";

    /// <summary>
    /// List of computed permission strings
    /// </summary>
    public List<string> Permissions { get; set; } = [];

    /// <summary>
    /// Timestamp when permissions were computed
    /// </summary>
    public DateTime CachedAt { get; set; }

    /// <summary>
    /// When these permissions expire
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// ETag for cache invalidation
    /// </summary>
    public string? ETag { get; set; }
}
