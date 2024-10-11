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
}