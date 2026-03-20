using System;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// AuthorizationData-related extension helpers.
/// </summary>
public static class AuthorizationDataExtensions
{
    private static readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _immediateTtl = TimeSpan.FromSeconds(1);

    extension(AuthorizationData authData)
    {
        /// <summary>
        /// Computes the TTL for the authorization data, ensuring it does not exceed the expiration time.
        /// If the authorization data is null, returns the default TTL.
        /// </summary>
        /// <param name="utcNow">The current UTC time.</param>
        /// <returns>The TTL for the authorization data.</returns>
        public TimeSpan ComputeAuthTtl(DateTime utcNow)
    {
        if (authData is null)
        {
            return _defaultTtl;
        }

        var authDataSafeTtl = authData.ExpiresAt - utcNow;
        return TimeSpan.FromSeconds(Math.Max(_immediateTtl.TotalSeconds, authDataSafeTtl.TotalSeconds));
    }

    /// <summary>
    /// Checks if the authorization data has the specified permission.
    /// </summary>
    /// <param name="permission">The permission to check for.</param>
    /// <returns>True if the authorization data has the specified permission, false otherwise.</returns>
    public bool HasPermission(string permission)
    {
        if (authData is null)
        {
            return false;
        }

        if (authData.IsAdmin)
        {
            return true;
        }

        return authData.Permissions.Contains(permission);
    }

    /// Checks if the authorization data has expired based on the current UTC time.
    /// </summary>
    /// <param name="permissions">The permissions to check for.</param>
    /// <returns>A dictionary of permissions and their presence in the authorization data.</returns>
    public Dictionary<string, bool> HasPermissions(IEnumerable<string> permissions)
    {
        if (authData is null)
        {
            return permissions.ToDictionary(p => p, _ => false);
        }

        if (authData.IsAdmin)
        {
            return permissions.ToDictionary(p => p, _ => true);
        }

        return permissions.ToDictionary(p => p, authData.Permissions.Contains);
    }
}
}