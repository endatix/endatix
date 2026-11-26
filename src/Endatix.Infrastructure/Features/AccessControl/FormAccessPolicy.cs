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
public sealed class FormAccessPolicy(ICurrentUserAuthorizationService authorizationService, HybridCache cache, IDateTimeProvider dateTimeProvider)
    : IResourceAccessQuery<FormAccessData, FormAccessContext>
{
    public async Task<Result<ICachedData<FormAccessData>>> GetAccessData(
        FormAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
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

        return await ReadAsync(new AuthorizedForm(context.FormId, access.Value), authData, cancellationToken);
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
    private Task<Result<ICachedData<FormAccessData>>> ReadAsync(AuthorizedForm form, AuthorizationData authData,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.Now.UtcDateTime;
        var lifetime = new PermissionCacheLifetime(authData, now);

        var cacheKey = $"auth:form_mgmt:{form.Id}:tenant:{authData.TenantId}:user:{authData.UserId}:{form.Access}";

        var projection = new PermissionProjection<FormAccessData>(cacheKey, form.Permissions,
            ["permissions", .. FormAccessCacheTags.ForFormAndAccess(form.Id)]);
        return lifetime.ReadAsync(cache, projection, cancellationToken);
    }
}

internal enum AccessLevel { View, Edit }

internal sealed record AuthorizedForm(long Id, AccessLevel Access)
{
    internal FormAccessData Permissions() => Access == AccessLevel.Edit
        ? FormAccessData.CreateWithEditAccess(Id)
        : FormAccessData.CreateWithViewAccess(Id);
}
