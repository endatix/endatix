using Ardalis.Specification;
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
/// Evaluates submission access for frontend/respondent: public form access and token-based access only.
/// No RBAC or identity; used for anonymous and token-based flows.
/// </summary>
public sealed class SubmissionPublicAccessPolicy(
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
        var cacheKey = $"auth:sub:public:{context.FormId}:{context.SubmissionId}:{context.AccessToken}";

        var cachedEnvelope = await cache.GetOrCreateAsync(
            key: cacheKey,
            factory: async ct => await ComputeAndWrapAsync(context, ct),
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
        CancellationToken cancellationToken)
    {
        var data = await ComputeAsync(context, cancellationToken);
        return new Cached<SubmissionAccessData>(
            data,
            TimeSpan.FromMinutes(CACHE_MINUTES),
            Guid.NewGuid().ToString("N")
        );
    }

    private async Task<SubmissionAccessData> ComputeAsync(
        SubmissionAccessContext context,
        CancellationToken cancellationToken)
    {
        var formPermissions = new HashSet<string>();
        var submissionPermissions = new HashSet<string>();

        var isFormPublic = await IsFormPublicAsync(context.FormId, cancellationToken);
        if (isFormPublic)
        {
            formPermissions.Add(ResourcePermissions.Form.View);
        }

        if (context.SubmissionId.HasValue && !string.IsNullOrEmpty(context.AccessToken))
        {
            var tokenResult = await tokenService.ResolveTokenAsync(context.AccessToken, cancellationToken);
            if (tokenResult.IsSuccess && tokenResult.Value == context.SubmissionId.Value)
            {
                submissionPermissions.UnionWith(ResourcePermissions.Submission.Sets.ReviewSubmission);
            }
        }
        else if (!context.SubmissionId.HasValue && isFormPublic)
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

    private async Task<bool> IsFormPublicAsync(long formId, CancellationToken cancellationToken)
    {
        var byIdSpec = new FormSpecifications.ById(formId);
        var isPublicDtoSpec = new FormProjections.IsPublicDtoSpec();
        var formDto = await formRepository.FirstOrDefaultAsync(byIdSpec.WithProjectionOf(isPublicDtoSpec), cancellationToken);
        return formDto?.IsPublic ?? false;
    }
}
