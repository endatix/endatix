namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Base class for access data
/// </summary>
public abstract class AccessDataBase : IAccessData
{
    /// <inheritdoc/>
    public abstract HashSet<string> Permissions { get; init; }

    /// <summary>
    /// The date and time the access data expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <inheritdoc/>
    public bool Has(string permission)
    {
        return Permissions.Contains(permission);
    }

    /// <inheritdoc/>
    public bool HasAny(IEnumerable<string> permissions)
    {
        return permissions.Any(Has);
    }

    /// <inheritdoc/>
    public bool HasAll(IEnumerable<string> permissions)
    {
        return permissions.All(Has);
    }
}
