using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
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
    IRepository<Form> formRepository,
    HybridCache cache,
    IDateTimeProvider dateTimeProvider
) : IResourceAccessQuery<PublicFormAccessData, SubmissionManagementAccessContext>
{
    private const int CACHE_MINUTES = 10;

    public async Task<Result<Cached<PublicFormAccessData>>> GetAccessData(
        SubmissionManagementAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            return Result<Cached<PublicFormAccessData>>.Error("Unauthorized");
        }

        var cacheKey = $"auth:sub:mgmt:{identityResult.Value.UserId}:{context.FormId}:{context.SubmissionId}";

        return await cache.GetOrCreateCachedResultAsync(
            cacheKey,
            async ct => 
            {
                var data = await ComputeAsync(context, identityResult.Value, ct);
                return Result.Success(data);
            },
            TimeSpan.FromMinutes(CACHE_MINUTES),
            dateTimeProvider.Now.UtcDateTime,
            tags: ["permissions", $"form:{context.FormId}"],
            cancellationToken: cancellationToken
        );
    }

    private async Task<PublicFormAccessData> ComputeAsync(
        SubmissionManagementAccessContext context,
        AuthorizationData identity,
        CancellationToken cancellationToken)
    {
        var formPermissions = new HashSet<string>();
        var submissionPermissions = new HashSet<string>();

        if (identity.IsAdmin)
        {
            formPermissions.UnionWith(ResourcePermissions.GetAllForResourceType(ResourceTypes.Form));

            submissionPermissions.UnionWith(ResourcePermissions.GetAllForResourceType(ResourceTypes.Submission));

            return new PublicFormAccessData
            {
                FormId = context.FormId.ToString(),
                SubmissionId = context.SubmissionId.ToString(),
                FormPermissions = formPermissions,
                SubmissionPermissions = submissionPermissions
            };
        }

        if (identity.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return new PublicFormAccessData
            {
                FormId = context.FormId.ToString(),
                SubmissionId = context.SubmissionId.ToString(),
                FormPermissions = formPermissions,
                SubmissionPermissions = submissionPermissions
            };
        }

        var hasFormEdit = await authorizationService.HasPermissionAsync(Actions.Forms.Edit, cancellationToken);
        if (hasFormEdit.IsSuccess && hasFormEdit.Value)
        {
            formPermissions.Add(ResourcePermissions.Form.Edit);
        }

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

        return new PublicFormAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = context.SubmissionId.ToString(),
            FormPermissions = formPermissions,
            SubmissionPermissions = submissionPermissions
        };
    }
}
