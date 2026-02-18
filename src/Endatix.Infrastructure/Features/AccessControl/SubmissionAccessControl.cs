using Ardalis.Specification;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Caching;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Features.Submissions;

public class SubmissionAccessControl(
    ICurrentUserAuthorizationService authorizationService,
    IRepository<Form> formRepository,
    ISubmissionTokenService tokenService,
    HybridCache cache
) : IResourceAccessStrategy<SubmissionAccessData, SubmissionAccessContext>
{
    private const int CACHE_MINUTES = 10;

    public async Task<Result<Cached<SubmissionAccessData>>> GetAccessData(
        SubmissionAccessContext context,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"auth:sub:{context.FormId}:{context.SubmissionId}:{context.AccessToken}";

        var cachedEnvelope = await cache.GetOrCreateAsync(
            key: cacheKey,
            factory: async ct => await GetFormAccessDataAsync(context, ct),
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

    private async Task<Cached<SubmissionAccessData>> GetFormAccessDataAsync(
        SubmissionAccessContext context,
        CancellationToken cancellationToken)
    {
        var data = await GetFormAccessDataInternalAsync(context, cancellationToken);

        return new Cached<SubmissionAccessData>(
            data,
            TimeSpan.FromMinutes(CACHE_MINUTES),
            Guid.NewGuid().ToString("N")
        );
    }

    private async Task<SubmissionAccessData> GetFormAccessDataInternalAsync(
        SubmissionAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            return new SubmissionAccessData
            {
                FormId = context.FormId.ToString(),
                SubmissionId = context.SubmissionId?.ToString()
            };
        }

        var identity = identityResult.Value;
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

        var isFormPublic = await IsFormPublicAsync(context.FormId, cancellationToken);
        if (isFormPublic)
        {
            formPermissions.Add(ResourcePermissions.Form.View);
        }

        await EvaluateFormLevelAccessAsync(formPermissions, cancellationToken);

        if (context.SubmissionId.HasValue)
        {
            await EvaluateSubmissionLevelAccessAsync(context, identity, submissionPermissions, cancellationToken);
        }
        else if (isFormPublic)
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

    private async Task EvaluateFormLevelAccessAsync(
        HashSet<string> permissions,
        CancellationToken cancellationToken)
    {
        var hasFormEdit = await authorizationService.HasPermissionAsync(Actions.Forms.Edit, cancellationToken);
        if (hasFormEdit.IsSuccess && hasFormEdit.Value)
        {
            permissions.Add(ResourcePermissions.Form.Edit);
        }
    }

    private async Task EvaluateSubmissionLevelAccessAsync(
        SubmissionAccessContext context,
        AuthorizationData identity,
        HashSet<string> permissions,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(context.AccessToken))
        {
            var tokenResult = await tokenService.ResolveTokenAsync(context.AccessToken, cancellationToken);
            if (tokenResult.IsSuccess && tokenResult.Value == context.SubmissionId.Value)
            {
                permissions.UnionWith(ResourcePermissions.Submission.Sets.ReviewSubmission);
            }
        }
        else if (identity.UserId != AuthorizationData.ANONYMOUS_USER_ID)
        {
            var hasSubmissionView = await authorizationService.HasPermissionAsync(Actions.Submissions.View, cancellationToken);
            if (hasSubmissionView.IsSuccess && hasSubmissionView.Value)
            {
                permissions.Add(ResourcePermissions.Submission.View);
            }

            var hasSubmissionEdit = await authorizationService.HasPermissionAsync(Actions.Submissions.Edit, cancellationToken);
            if (hasSubmissionEdit.IsSuccess && hasSubmissionEdit.Value)
            {
                permissions.UnionWith(ResourcePermissions.Submission.Sets.EditSubmission);
            }

            var hasSubmissionDelete = await authorizationService.HasPermissionAsync(Actions.Submissions.Delete, cancellationToken);
            if (hasSubmissionDelete.IsSuccess && hasSubmissionDelete.Value)
            {
                permissions.Add(ResourcePermissions.Submission.DeleteFile);
            }
        }
    }

    private async Task<bool> IsFormPublicAsync(long formId, CancellationToken cancellationToken)
    {
        var byIdSpec = new FormSpecifications.ById(formId);
        var isPublicDtoSpec = new FormProjections.IsPublicDtoSpec();

        var formDto = await formRepository.FirstOrDefaultAsync(byIdSpec.WithProjectionOf(isPublicDtoSpec), cancellationToken);
        return formDto?.IsPublic ?? false;
    }
}
