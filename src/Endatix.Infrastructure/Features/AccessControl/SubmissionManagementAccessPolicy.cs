using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Evaluates submission access for backend/admin: RBAC-based form and submission permissions.
/// Used when the current user is authenticated; no token or public-form logic.
/// </summary>
public sealed class SubmissionManagementAccessPolicy(
    ICurrentUserAuthorizationService authorizationService,
    HybridCache cache,
    IDateTimeProvider dateTimeProvider
) : IResourceAccessQuery<SubmissionManagementAccessData, SubmissionManagementAccessContext>
{
    public async Task<Result<Cached<SubmissionManagementAccessData>>> GetAccessData(
        SubmissionManagementAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            return identityResult.ToErrorResult<Cached<SubmissionManagementAccessData>>();
        }

        var authData = identityResult.Value;
        var now = dateTimeProvider.Now.UtcDateTime;
        var ttl = authData.ComputeAuthTtl(now);
        var cacheKey =
            $"auth:sb_mgmt:form:{context.FormId}:sub:{context.SubmissionId}:user:{authData.UserId}";

        return await cache.GetOrCreateCachedResultAsync(
            key: cacheKey,
            factory: _ => Task.FromResult(ComputeResourceAccess(context, authData)),
            ttl: ttl,
            utcNow: now,
            tags: ["permissions", $"submission:{context.SubmissionId}"],
            cancellationToken: cancellationToken
        );
    }

    private Result<SubmissionManagementAccessData> ComputeResourceAccess(
        SubmissionManagementAccessContext context,
        AuthorizationData authData)
    {
        if (authData is null || authData.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unauthorized("You are not authorized to access this submission.");
        }

        if (!authData.Permissions.Contains(Actions.Access.Hub))
        {
            return Result.Forbidden("You are not authorized to access this submission.");
        }

        if (authData.IsAdmin || authData.Permissions.Contains(Actions.Submissions.Edit))
        {
            return SubmissionManagementAccessData.CreateWithEditAccess(context.FormId, context.SubmissionId);
        }

        if (authData.Permissions.Contains(Actions.Submissions.View))
        {
            return SubmissionManagementAccessData.CreateWithViewAccess(context.FormId, context.SubmissionId);
        }

        return Result.Forbidden("You are not authorized to access this submission.");
    }
}
