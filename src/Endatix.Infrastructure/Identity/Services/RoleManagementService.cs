using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity.Services;

/// <summary>
/// Service for managing user roles.
/// </summary>
public class RoleManagementService(UserManager<AppUser> userManager) : IRoleManagementService
{
    /// <inheritdoc/>
    public async Task<Result> AssignRoleToUserAsync(long userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.NotFound($"User with ID {userId} not found.");
        }

        var isInRole = await userManager.IsInRoleAsync(user, roleName);
        if (isInRole)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"User already has role '{roleName}'."
            });
        }

        var result = await userManager.AddToRoleAsync(user, roleName);
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
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.NotFound($"User with ID {userId} not found.");
        }

        var isInRole = await userManager.IsInRoleAsync(user, roleName);
        if (!isInRole)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"User does not have role '{roleName}'."
            });
        }

        var result = await userManager.RemoveFromRoleAsync(user, roleName);
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
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<IList<string>>.NotFound($"User with ID {userId} not found.");
        }

        var roles = await userManager.GetRolesAsync(user);

        return Result<IList<string>>.Success(roles);
    }
}
