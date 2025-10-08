using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Services;

/// <summary>
/// Service for managing user roles.
/// </summary>
public class RoleManagementService : IRoleManagementService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppIdentityDbContext _identityDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRolesRepository _rolesRepository;

    public RoleManagementService(
        UserManager<AppUser> userManager,
        AppIdentityDbContext identityDbContext,
        ITenantContext tenantContext,
        IRolesRepository rolesRepository)
    {
        _userManager = userManager;
        _identityDbContext = identityDbContext;
        _tenantContext = tenantContext;
        _rolesRepository = rolesRepository;
    }
    /// <inheritdoc/>
    public async Task<Result> AssignRoleToUserAsync(long userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.NotFound($"User with ID {userId} not found.");
        }

        var isInRole = await _userManager.IsInRoleAsync(user, roleName);
        if (isInRole)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"User already has role '{roleName}'."
            });
        }

        var result = await _userManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            var errorMessages = result.Errors.Select(e => e.Description);
            return Result.Error(new ErrorList(errorMessages));
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> RemoveRoleFromUserAsync(long userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.NotFound($"User with ID {userId} not found.");
        }

        var isInRole = await _userManager.IsInRoleAsync(user, roleName);
        if (!isInRole)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"User does not have role '{roleName}'."
            });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            var errorMessages = result.Errors.Select(e => e.Description);
            return Result.Error(new ErrorList(errorMessages));
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<IList<string>>> GetUserRolesAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<IList<string>>.NotFound($"User with ID {userId} not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Result<IList<string>>.Success(roles);
    }

    /// <inheritdoc/>
    public async Task<Result<string>> CreateRoleAsync(string name, string? description, List<string> permissionNames, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;

        var existingRole = await _identityDbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == name && r.TenantId == tenantId, cancellationToken);

        if (existingRole != null)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(name),
                ErrorMessage = $"Role '{name}' already exists for this tenant."
            });
        }

        var permissions = await _identityDbContext.Permissions
            .AsNoTracking()
            .Where(p => permissionNames.Contains(p.Name))
            .ToListAsync(cancellationToken);

        if (permissions.Count != permissionNames.Count)
        {
            var foundPermissionNames = permissions.Select(p => p.Name);
            var missingPermissions = permissionNames.Except(foundPermissionNames);

            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(permissionNames),
                ErrorMessage = $"The following permissions do not exist: {string.Join(", ", missingPermissions)}"
            });
        }

        var role = new AppRole
        {
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Description = description,
            TenantId = tenantId,
            IsSystemDefined = false,
            IsActive = true
        };

        var permissionIds = permissions.Select(p => p.Id).ToList();
        var createdRole = await _rolesRepository.CreateRoleWithPermissionsAsync(role, permissionIds, cancellationToken);

        return Result<string>.Created(createdRole.Id.ToString());
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;

        var role = await _identityDbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == roleName && r.TenantId == tenantId, cancellationToken);

        if (role == null)
        {
            return Result.NotFound($"Role '{roleName}' not found for this tenant.");
        }

        if (role.IsSystemDefined)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"Cannot delete system-defined role '{roleName}'."
            });
        }

        var isRoleAssignedToUsers = await _identityDbContext.UserRoles
            .AnyAsync(ur => ur.RoleId == role.Id, cancellationToken);

        if (isRoleAssignedToUsers)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"Cannot delete role '{roleName}' because it is assigned to one or more users."
            });
        }

        await _rolesRepository.DeleteRoleAsync(role, cancellationToken);

        return Result.Success();
    }
}
