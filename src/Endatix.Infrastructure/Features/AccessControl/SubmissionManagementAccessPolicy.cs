using Ardalis.Specification;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization;
using Endatix.Core.Authorization.Models;
using Endatix.Core.Authorization.Permissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Caching;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Evaluates submission access for backend/admin: RBAC-based form and submission permissions.
/// Used when the current user is authenticated; no token or public-form logic.
/// </summary>
public sealed class SubmissionManagementAccessPolicy(
    ICurrentUserAuthorizationService authorizationService,
    IRepository<Form> formRepository,
    HybridCache cache
) : IResourceAccessStrategy<SubmissionAccessData, SubmissionAccessContext>
{
    private const int CACHE_MINUTES = 10;

    public async Task<Result<Cached<SubmissionAccessData>>> GetAccessData(
        SubmissionAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            return Result<Cached<SubmissionAccessData>>.Error("Unauthorized");
        }

        var cacheKey = $"auth:sub:mgmt:{identityResult.Value.UserId}:{context.FormId}:{context.SubmissionId}";

        var cachedEnvelope = await cache.GetOrCreateAsync(
            key: cacheKey,
            factory: async ct => await ComputeAndWrapAsync(context, identityResult.Value, ct),
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(CACHE_MINUTES),
                LocalCacheExpiration = TimeSpan.FromMinutes(CACHE_MINUTES)
            },
            tags: ["permissions", $"form:{context.FormId}"],
            cancellationToken: cancellationToken
        );

        return cachedEnvelope != null
            ? Result<Cached<SubmissionAccessData>>.Success(cachedEnvelope)
            : Result<Cached<SubmissionAccessData>>.Error("Failed to compute access");
    }

    private async Task<Cached<SubmissionAccessData>> ComputeAndWrapAsync(
        SubmissionAccessContext context,
        AuthorizationData identity,
        CancellationToken cancellationToken)
    {
        var data = await ComputeAsync(context, identity, cancellationToken);
        return new Cached<SubmissionAccessData>(
            data,
            TimeSpan.FromMinutes(CACHE_MINUTES),
            Guid.NewGuid().ToString("N")
        );
    }

    private async Task<SubmissionAccessData> ComputeAsync(
        SubmissionAccessContext context,
        AuthorizationData identity,
        CancellationToken cancellationToken)
    {
        var formPermissions = new HashSet<string>();
        var submissionPermissions = new HashSet<string>();

        if (identity.IsAdmin)
        {
            formPermissions.UnionWith(ResourcePermissions.GetAllForResourceType(ResourceTypes.Form));
            if (context.SubmissionId.HasValue)
            {
                submissionPermissions.UnionWith(ResourcePermissions.GetAllForResourceType(ResourceTypes.Submission));
            }
            else
            {
                submissionPermissions.Add(ResourcePermissions.Submission.Create);
                submissionPermissions.Add(ResourcePermissions.Submission.UploadFile);
            }

            return new SubmissionAccessData
            {
                FormId = context.FormId.ToString(),
                SubmissionId = context.SubmissionId?.ToString(),
                FormPermissions = formPermissions,
                SubmissionPermissions = submissionPermissions
            };
        }

        if (identity.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return new SubmissionAccessData
            {
                FormId = context.FormId.ToString(),
                SubmissionId = context.SubmissionId?.ToString(),
                FormPermissions = formPermissions,
                SubmissionPermissions = submissionPermissions
            };
        }

        var hasFormEdit = await authorizationService.HasPermissionAsync(Actions.Forms.Edit, cancellationToken);
        if (hasFormEdit.IsSuccess && hasFormEdit.Value)
        {
            formPermissions.Add(ResourcePermissions.Form.Edit);
        }

        if (context.SubmissionId.HasValue)
        {
            var hasSubmissionView = await authorizationService.HasPermissionAsync(Actions.Submissions.View, cancellationToken);
            if (hasSubmissionView.IsSuccess && hasSubmissionView.Value)
            {
                submissionPermissions.Add(ResourcePermissions.Submission.View);
            }

            var hasSubmissionEdit = await authorizationService.HasPermissionAsync(Actions.Submissions.Edit, cancellationToken);
            if (hasSubmissionEdit.IsSuccess && hasSubmissionEdit.Value)
            {
                submissionPermissions.UnionWith(ResourcePermissions.Submission.Sets.EditSubmission);
            }

            var hasSubmissionDelete = await authorizationService.HasPermissionAsync(Actions.Submissions.Delete, cancellationToken);
            if (hasSubmissionDelete.IsSuccess && hasSubmissionDelete.Value)
            {
                submissionPermissions.Add(ResourcePermissions.Submission.DeleteFile);
            }
        }

        return new SubmissionAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = context.SubmissionId?.ToString(),
            FormPermissions = formPermissions,
            SubmissionPermissions = submissionPermissions
        };
    }
}
