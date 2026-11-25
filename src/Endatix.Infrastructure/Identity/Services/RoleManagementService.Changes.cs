using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Services;

public sealed partial class RoleManagementService
{
    private async Task<Result> ChangePlatformRoleAsync(AppUser user, Func<AppUser, string, Task<IdentityResult>> change, CancellationToken ct)
    {
        var result = await change(user, SystemRole.PlatformAdmin.Name);
        if (!result.Succeeded)
        {
            return Result.Error(new ErrorList(result.Errors.Select(e => e.Description)));
        }
        await InvalidateUserAuthorizationAsync(user, ct);
        return Result.Success();
    }

    private async Task<Result<List<Permission>>> ReadRolePermissionsAsync(List<string> names, CancellationToken ct)
    {
        var permissions = names.Count == 0 ? [] : await _identityDbContext.Permissions.AsNoTracking()
            .Where(permission => names.Contains(permission.Name)).ToListAsync(ct);
        var valid = EnsureAllPermissionsExist(names, permissions);
        return valid.IsSuccess ? Result.Success(permissions) : valid.ToErrorResult<List<Permission>>();
    }

    private async Task<Result<AppRole>> GetAuthorizedRoleForUpdateAsync(string roleName, List<string> names, CancellationToken ct)
    {
        var normalizedName = NormalizeRoleName(roleName);
        var tenantId = _tenantContext.TenantId;
        var target = await _identityDbContext.Roles.FirstOrDefaultAsync(
            role => role.NormalizedName == normalizedName && role.TenantId == tenantId, ct);
        if (target is null)
        {
            return Result.NotFound($"Role '{roleName}' not found for this tenant.");
        }
        if (target.IsSystemDefined)
        {
            return Result.Invalid(new ValidationError { Identifier = nameof(roleName), ErrorMessage = $"Cannot update system-defined role '{roleName}'." });
        }
        var access = await EnsureCanRewriteRoleAsync(target.Id, names, ct);
        return access.IsSuccess ? Result.Success(target) : access.ToErrorResult<AppRole>();
    }

    private async Task<Result> SaveAssignmentsAsync(IEnumerable<IdentityUserRole<long>> removed, IEnumerable<IdentityUserRole<long>> added, CancellationToken ct)
    {
        _identityDbContext.UserRoles.RemoveRange(removed);
        _identityDbContext.UserRoles.AddRange(added);
        await _identityDbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> DeleteCustomRoleAsync(AppRole role, CancellationToken ct)
    {
        await _rolesRepository.DeleteRoleAsync(role, ct);
        return Result.Success();
    }
}
