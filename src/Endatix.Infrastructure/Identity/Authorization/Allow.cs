namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Static class to act as a strongly typed Permission store
/// </summary>
public static class Allow
{
    /// <summary>
    /// Permission allowing access to all resources (legacy - use specific Actions instead)
    /// </summary>
    [Obsolete("Use specific permissions from Permissions class instead")]
    public const string AllowAll = "allow.all";
}