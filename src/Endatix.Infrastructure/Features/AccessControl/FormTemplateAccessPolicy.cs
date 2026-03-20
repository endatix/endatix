using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Evaluates backend/admin access for a specific form template (RBAC-based).
/// </summary>
public sealed class FormTemplateAccessPolicy(
    ICurrentUserAuthorizationService authorizationService,
    HybridCache cache,
    IDateTimeProvider dateTimeProvider
) : IResourceAccessQuery<FormTemplateAccessData, FormTemplateAccessContext>
{
    public async Task<Result<Cached<FormTemplateAccessData>>> GetAccessData(
        FormTemplateAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            return identityResult.ToErrorResult<Cached<FormTemplateAccessData>>();
        }

        var authData = identityResult.Value;
        var now = dateTimeProvider.Now.UtcDateTime;
        var ttl = authData.ComputeAuthTtl(now);

        var cacheKey = $"auth:tpl_mgmt:{context.TemplateId}:user:{authData.UserId}";

        return await cache.GetOrCreateCachedResultAsync(
            key: cacheKey,
            factory: _ => Task.FromResult(ComputeResourceAccess(context, authData)),
            ttl: ttl,
            utcNow: now,
            tags: ["permissions", $"template:{context.TemplateId}"],
            cancellationToken: cancellationToken);
    }

    private static Result<FormTemplateAccessData> ComputeResourceAccess(
        FormTemplateAccessContext context,
        AuthorizationData authData)
    {
        if (authData is null || authData.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unauthorized("You are not authorized to access this form template.");
        }

        if (!authData.Permissions.Contains(Actions.Access.Hub))
        {
            return Result.Forbidden("You are not authorized to access this form template.");
        }

        if (authData.IsAdmin || authData.Permissions.Contains(Actions.Templates.Edit))
        {
            return FormTemplateAccessData.CreateWithEditAccess(context.TemplateId);
        }

        if (authData.Permissions.Contains(Actions.Templates.View))
        {
            return FormTemplateAccessData.CreateWithViewAccess(context.TemplateId);
        }

        return Result.Forbidden("You are not authorized to access this form template.");
    }
}
