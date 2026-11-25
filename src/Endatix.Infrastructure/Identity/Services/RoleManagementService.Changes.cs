using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Identity.Services;

public sealed partial class RoleManagementService
{
    private AccountStanding _standing = new();

    internal static RoleManagementService Create(IServiceProvider services)
    {
        var roles = ActivatorUtilities.CreateInstance<RoleManagementService>(services);
        roles._standing = services.GetRequiredService<AccountStanding>();
        return roles;
    }

    private async Task<Result<string>?> CheckRolePermissionsAsync(IReadOnlyCollection<string> names, IReadOnlyCollection<Permission> permissions, CancellationToken ct)
    {
        var valid = EnsureAllPermissionsExist(names, permissions);
        return valid.IsSuccess ? await _standing.RefusalAsync<string>(ct) : valid.ToErrorResult<string>();
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
