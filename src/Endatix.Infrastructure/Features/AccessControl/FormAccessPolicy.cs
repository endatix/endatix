using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Evaluates backend/admin access for a specific form (RBAC-based).
/// </summary>
public sealed class FormAccessPolicy(
    ICurrentUserAuthorizationService authorizationService,
    HybridCache cache,
    IDateTimeProvider dateTimeProvider
) : IResourceAccessQuery<FormAccessData, FormAccessContext>
{
    public async Task<Result<Cached<FormAccessData>>> GetAccessData(
        FormAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            return identityResult.ToErrorResult<Cached<FormAccessData>>();
        }

        var authData = identityResult.Value;
        var now = dateTimeProvider.Now.UtcDateTime;
        var ttl = authData.ComputeAuthTtl(now);

        var cacheKey = $"auth:form_mgmt:{context.FormId}:user:{authData.UserId}";

        return await cache.GetOrCreateCachedResultAsync(
            key: cacheKey,
            factory: _ => Task.FromResult(ComputeResourceAccess(context, authData)),
            ttl: ttl,
            utcNow: now,
            tags: ["permissions", $"form:{context.FormId}"],
            cancellationToken: cancellationToken);
    }

    private static Result<FormAccessData> ComputeResourceAccess(
        FormAccessContext context,
        AuthorizationData authData)
    {
        if (authData is null || authData.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unauthorized("You are not authorized to access this form.");
        }

        if (!authData.Permissions.Contains(Actions.Access.Hub))
        {
            return Result.Forbidden("You are not authorized to access this form.");
        }

        if (authData.IsAdmin || authData.Permissions.Contains(Actions.Forms.Edit))
        {
            return FormAccessData.CreateWithEditAccess(context.FormId);
        }

        if (authData.Permissions.Contains(Actions.Forms.View))
        {
            return FormAccessData.CreateWithViewAccess(context.FormId);
        }

        return Result.Forbidden("You are not authorized to access this form.");
    }
}
