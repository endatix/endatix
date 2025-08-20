using Endatix.Core.Entities.Identity;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Provides access to the current user context.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Returns true if the current user is anonymous (not authenticated).
    /// </summary>
    bool IsAnonymous { get; }

    /// <summary>
    /// Returns true if the current user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the current user, or null if not authenticated.
    /// </summary>
    User? GetCurrentUser();

    /// <summary>
    /// Gets the current user's ID, or null if not authenticated.
    /// </summary>
    string? GetCurrentUserId();

    /// <summary>
    /// Gets the current user's display name from the Name claim, or null if not available.
    /// </summary>
    string? GetCurrentUserName();
}