namespace Endatix.Infrastructure.Identity.Repositories;

/// <summary>
/// Repository for managing roles and their associated permissions.
/// Note: Interface is in Infrastructure (not Core) because AppRole is an Infrastructure entity (ASP.NET Identity wrapper).
/// Does not inherit from IRepository because AppRole does not implement IAggregateRoot (it's an ASP.NET Identity entity).
/// </summary>
public interface IRolesRepository
{
    /// <summary>
    /// Creates a new role with its permissions asynchronously.
    /// Handles transaction management and ID generation internally.
    /// </summary>
    /// <param name="role">The role to be created.</param>
    /// <param name="permissionIds">The IDs of permissions to assign to the role.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created role with generated ID.</returns>
    Task<AppRole> CreateRoleWithPermissionsAsync(AppRole role, List<long> permissionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a role and its associated permissions asynchronously.
    /// Handles transaction management internally.
    /// </summary>
    /// <param name="role">The role to be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteRoleAsync(AppRole role, CancellationToken cancellationToken = default);
}
