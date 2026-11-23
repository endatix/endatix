using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Assigns a role on behalf of the system, where no signed-in user acts: self-registration, the initial user seed,
/// tenant provisioning. It skips the grant ceiling that <see cref="IRoleManagementService"/> applies to a user, so
/// only code that grants a role by design takes this dependency; never call it with a role a request asked for.
/// </summary>
public interface ISystemRoleAssignment
{
    /// <summary>
    /// Assigns a role to a user without checking the current user's access.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roleName">The name of the role to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result> AssignRoleAsSystemAsync(long userId, string roleName, CancellationToken cancellationToken = default);
}
