using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Evaluates submission access for backend/admin: RBAC-based form and submission permissions.
/// Used when the current user is authenticated; no token or public-form logic.
/// </summary>
public sealed class SubmissionAccessPolicy(
    ICurrentUserAuthorizationService authorizationService,
    IRepository<Submission> submissionRepository,
    HybridCache cache,
    IDateTimeProvider dateTimeProvider
) : IResourceAccessQuery<SubmissionAccessData, SubmissionAccessContext>
{
    /// <inheritdoc/>
    public async Task<Result<ICachedData<SubmissionAccessData>>> GetAccessData(
        SubmissionAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            return identityResult.ToErrorResult<ICachedData<SubmissionAccessData>>();
        }

        var authData = identityResult.Value;
        var now = dateTimeProvider.Now.UtcDateTime;
        var ttl = authData.ComputeAuthTtl(now);
        var cacheKey =
            $"auth:sb_mgmt:form:{context.FormId}:sub:{context.SubmissionId}:user:{authData.UserId}";

        return await cache.GetOrCreateCachedResultAsync(
            key: cacheKey,
            factory: ct => ComputeResourceAccessAsync(context, authData, ct),
            ttl: ttl,
            utcNow: now,
            tags: ["permissions", $"submission:{context.SubmissionId}"],
            cancellationToken: cancellationToken
        );
    }

    private async Task<Result<SubmissionAccessData>> ComputeResourceAccessAsync(
        SubmissionAccessContext context,
        AuthorizationData authData,
        CancellationToken cancellationToken)
    {
        var relationResult = await ValidateSubmissionRelationAsync(
            context.FormId,
            context.SubmissionId,
            cancellationToken);
        if (!relationResult.IsSuccess)
        {
            return relationResult.ToErrorResult<SubmissionAccessData>();
        }

        if (authData is null || authData.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unauthorized("You are not authorized to access this submission.");
        }

        if (!authData.HasPermission(Actions.Access.Hub))
        {
            return Result.Forbidden("You are not authorized to access this submission.");
        }

        if (authData.HasPermission(Actions.Submissions.Edit))
        {
            return SubmissionAccessData.CreateWithEditAccess(context.FormId, context.SubmissionId);
        }

        if (authData.HasPermission(Actions.Submissions.View))
        {
            return SubmissionAccessData.CreateWithViewAccess(context.FormId, context.SubmissionId);
        }

        return Result.Forbidden("You are not authorized to access this submission.");
    }

    private async Task<Result<bool>> ValidateSubmissionRelationAsync(
        long formId,
        long submissionId,
        CancellationToken cancellationToken)
    {
        var spec = new SubmissionByFormIdAndSubmissionIdSpec(formId, submissionId);
        var relationExists = await submissionRepository.AnyAsync(spec, cancellationToken);
        return relationExists
            ? Result.Success(true)
            : Result.NotFound("Submission not found");
    }
}
