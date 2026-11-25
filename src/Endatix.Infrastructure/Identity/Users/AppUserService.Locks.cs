using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity.Users;

public sealed partial class AppUserService
{
    /// <inheritdoc />
    public Task<Result> LockoutUserAsync(long userId, CancellationToken cancellationToken = default) =>
        userId > 0 && IsCurrentUser(userId)
            ? Task.FromResult(Result.Forbidden(SELF_LOCKOUT_FORBIDDEN_MESSAGE))
            : ChangeLockAsync(userId, LockAsync, cancellationToken);

    /// <inheritdoc />
    public Task<Result> UnlockUserAsync(long userId, CancellationToken cancellationToken = default) =>
        ChangeLockAsync(userId, user => userManager.SetLockoutEndDateAsync(user, null), cancellationToken);

    private async Task<IdentityResult> LockAsync(AppUser user)
    {
        var enabled = await userManager.SetLockoutEnabledAsync(user, true);
        return enabled.Succeeded ? await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue) : enabled;
    }

    private async Task<Result> ChangeLockAsync(long userId, Func<AppUser, Task<IdentityResult>> change, CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId;
        var user = userId > 0 ? await FindCurrentTenantUserAsync(userId, tenantId, ct) : null;
        if (user is null)
        {
            return Result.NotFound();
        }
        var access = await EnsureCallerCoversUserAccessAsync(user, ct);
        if (!access.IsSuccess)
        {
            return access;
        }
        return await ApplyLockChangeAsync(user, change, ct);
    }

    private async Task<Result> ApplyLockChangeAsync(AppUser user, Func<AppUser, Task<IdentityResult>> change, CancellationToken ct)
    {
        var changed = await change(user);
        if (!changed.Succeeded)
        {
            return ToErrorResult(changed);
        }
        await authorizationCache.InvalidateAsync(user.Id.ToString(), user.TenantId, ct);
        return Result.Success();
    }
}
