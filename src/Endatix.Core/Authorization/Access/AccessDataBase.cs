using System.Collections.Immutable;

namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Base class for access data
/// </summary>
public abstract class AccessDataBase : IAccessData
{
    protected static readonly ImmutableHashSet<string> EmptyPermissions = ImmutableHashSet<string>.Empty;

    protected static ImmutableHashSet<string> ToImmutableSet(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return permissions.ToImmutableHashSet();
    }

    /// <inheritdoc/>
    public abstract ImmutableHashSet<string> Permissions { get; init; }

    /// <summary>
    /// The date and time the access data expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <inheritdoc/>
    public bool Has(string permission)
        => Permissions.Contains(permission);

    /// <inheritdoc/>
    public bool HasAny(IEnumerable<string> permissions)
        => permissions.Any(Has);

    /// <inheritdoc/>
    public bool HasAll(IEnumerable<string> permissions)
        => permissions.All(Has);
}
