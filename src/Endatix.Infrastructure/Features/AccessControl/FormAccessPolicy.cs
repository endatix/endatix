using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Evaluates backend/admin access for a specific form (RBAC-based).
/// </summary>
public sealed class FormAccessPolicy : IResourceAccessQuery<FormAccessData, FormAccessContext>
{
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly FormPermissionCache _permissions;

    private FormAccessPolicy(ICurrentUserAuthorizationService authorizationService, FormPermissionCache permissions)
    {
        _authorizationService = authorizationService;
        _permissions = permissions;
    }

    /// <summary>Constructs the form permission policy.</summary>
    public FormAccessPolicy(ICurrentUserAuthorizationService authorizationService, HybridCache cache, IDateTimeProvider dateTimeProvider)
        : this(authorizationService, new FormPermissionCache(cache, dateTimeProvider)) { }

    public async Task<Result<ICachedData<FormAccessData>>> GetAccessData(
        FormAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await _authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            return identityResult.ToErrorResult<ICachedData<FormAccessData>>();
        }

        var authData = identityResult.Value;
        var access = AuthorizedProjection(authData);
        if (!access.IsSuccess)
        {
            return access.ToErrorResult<ICachedData<FormAccessData>>();
        }

        return await _permissions.ReadAsync(new AuthorizedForm(context.FormId, access.Value), authData, cancellationToken);
    }

    private static Result<AccessLevel> AuthorizedProjection(AuthorizationData authData)
    {
        const string denied = "You are not authorized to access this form.";
        if (authData is null || authData.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unauthorized(denied);
        }
        if (!authData.HasPermission(Actions.Access.Hub))
        {
            return Result.Forbidden(denied);
        }
        if (authData.HasPermission(Actions.Forms.Edit))
        {
            return AccessLevel.Edit;
        }
        return authData.HasPermission(Actions.Forms.View) ? Result.Success(AccessLevel.View) : Result.Forbidden(denied);
    }
}

internal enum AccessLevel { View, Edit }

internal sealed record AuthorizedForm(long Id, AccessLevel Access)
{
    internal FormAccessData Permissions() => Access == AccessLevel.Edit
        ? FormAccessData.CreateWithEditAccess(Id)
        : FormAccessData.CreateWithViewAccess(Id);
}

internal sealed class FormPermissionCache(HybridCache cache, IDateTimeProvider dateTimeProvider)
{
    internal async Task<Result<ICachedData<FormAccessData>>> ReadAsync(AuthorizedForm form, AuthorizationData authData,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.Now.UtcDateTime;
        var lifetime = new PermissionCacheLifetime(authData, now);

        var cacheKey = $"auth:form_mgmt:{form.Id}:tenant:{authData.TenantId}:user:{authData.UserId}:{form.Access}";

        var entry = await cache.GetOrCreateAsync(cacheKey,
            _ => ValueTask.FromResult(new Cached<FormAccessData>(form.Permissions(), now, lifetime.Ttl)),
            options: new HybridCacheEntryOptions { Expiration = lifetime.Ttl },
            tags: ["permissions", .. FormAccessCacheTags.ForFormAndAccess(form.Id)],
            cancellationToken: cancellationToken);

        return Result.Success<ICachedData<FormAccessData>>(lifetime.Cap(entry));
    }
}
