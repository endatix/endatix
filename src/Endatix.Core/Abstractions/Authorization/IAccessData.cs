namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// Interface for access data that contains permission evaluation methods.
/// </summary>
public interface IAccessData
{
    /// <summary>
    /// The permissions associated with the access data.
    /// They are used to provide a list of permissions that the user has access to for that resource.
    /// </summary>
    HashSet<string> Permissions { get; }

    /// <summary>
    /// Checks if the specific permission is present.
    /// </summary>
    bool Has(string permission);

    /// <summary>
    /// Checks if any of the specified permissions are present.
    /// </summary>
    bool HasAny(IEnumerable<string> permissions);

    /// <summary>
    /// Checks if all of the specified permissions are present.
    /// </summary>
    bool HasAll(IEnumerable<string> permissions);
}
