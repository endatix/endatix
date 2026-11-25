using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Users;

public sealed partial class AppUserService
{
    private readonly AccountStanding _standing = new();

    /// <summary>Creates the user service with the host's scoped account standing.</summary>
    public AppUserService(UserManager<AppUser> userManager, ITenantContext tenantContext,
        AppIdentityDbContext identityDbContext, IEmailVerificationService emailVerificationService,
        IUserContext userContext, IRelationalSubstringLikeFilter substringLikeFilter,
        IAuthorizationCache authorizationCache, IRoleManagementService roleManagementService,
        ILogger<AppUserService> logger, AccountStanding standing)
        : this(userManager, tenantContext, identityDbContext, emailVerificationService, userContext,
            substringLikeFilter, authorizationCache, roleManagementService, logger) => _standing = standing;

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
        return await _standing.RefusalAsync(ct) ?? await ApplyLockChangeAsync(user, change, ct);
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
