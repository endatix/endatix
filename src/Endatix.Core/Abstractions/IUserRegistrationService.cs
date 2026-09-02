using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Defines the contract for a service responsible for user registration operations.
/// </summary>
public interface IUserRegistrationService
{
    /// <summary>
    /// Registers a new user with the provided email and password.
    /// </summary>
    /// <param name="email">The email address of the user to be registered.</param>
    /// <param name="password">The password for the new user account.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A a Task with Result of the registered User if successful.</returns>
    Task<Result<User>> RegisterUserAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Registers a new user with the provided email, password, and additional options.
    /// </summary>
    /// <param name="email">The email address of the user to be registered.</param>
    /// <param name="password">The password for the new user account.</param>
    /// <param name="tenantId">The tenant ID to assign to the user.</param>
    /// <param name="isEmailConfirmed">Whether the email should be marked as confirmed.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A a Task with Result of the registered User if successful.</returns>
    Task<Result<User>> RegisterUserAsync(string email, string password, long tenantId, bool isEmailConfirmed, CancellationToken cancellationToken);

    /// <summary>
    /// Registers or reattaches a pending invited user and sends a tenant invitation activation email when needed.
    /// </summary>
    Task<Result<User>> RegisterInvitedUserAsync(string email, long tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Registers a tenant member and grants <paramref name="roleName"/> atomically: either both land or
    /// neither does, so a failed grant cannot leave an account that no retry can repair. The verification
    /// email is sent after the write commits, because it cannot be rolled back.
    /// </summary>
    /// <param name="email">The email of the user to register.</param>
    /// <param name="password">The chosen password.</param>
    /// <param name="tenantId">The tenant the user is created in.</param>
    /// <param name="roleName">The role granted on registration. Must already be assignable.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<Result<User>> RegisterTenantUserAsync(
        string email,
        string password,
        long tenantId,
        string roleName,
        CancellationToken cancellationToken);
}