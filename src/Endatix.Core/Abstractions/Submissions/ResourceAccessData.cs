using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Composite authorization data that combines identity (RBAC) with context-specific scopes (ReBAC).
/// This follows the "Cached Identity, Dynamic Scopes" pattern:
/// - Identity: Stable, User-Centric, Long Cache (Session)
/// - Scopes: Volatile, Request-Centric, Computed on demand
/// </summary>
public class ResourceAccessData
{
    /// <summary>
    /// The cached identity (RBAC) - represents "Who the user is"
    /// </summary>
    public AuthorizationData Identity { get; init; } = AuthorizationData.ForAnonymousUser(0);

    /// <summary>
    /// The computed context (ReBAC) - represents "What the user can do here"
    /// </summary>
    public List<ResourceScope> Scopes { get; init; } = [];

    /// <summary>
    /// Checks if the user has a specific permission for a given resource.
    /// Admin users always return true.
    /// </summary>
    /// <param name="resourceType">The type of resource (e.g., "form", "submission")</param>
    /// <param name="resourceId">The ID of the resource</param>
    /// <param name="permission">The permission to check for</param>
    /// <returns>True if user has the permission, false otherwise</returns>
    public bool Can(string resourceType, string resourceId, string permission)
    {
        if (Identity.IsAdmin)
        {
            return true;
        }

        return Scopes.Any(s =>
            s.ResourceType == resourceType &&
            s.ResourceId == resourceId &&
            s.Permissions.Contains(permission));
    }

    /// <summary>
    /// Checks if user has permission on any scope of a given type
    /// </summary>
    public bool CanAny(string resourceType, string permission)
    {
        if (Identity.IsAdmin)
        {
            return true;
        }


        return Scopes.Any(s =>
            s.ResourceType == resourceType &&
            s.Permissions.Contains(permission));
    }
}
