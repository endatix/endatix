using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization;
using Endatix.Core.Authorization.Models;
using Endatix.Core.Infrastructure.Caching;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Composite strategy for frontend submission access: merges public/token (SubmissionPublicAccessPolicy)
/// and authenticated RBAC (SubmissionManagementAccessPolicy) so the client gets a single permission set.
/// </summary>
public sealed class SubmissionAccessStrategy(
    SubmissionPublicAccessPolicy publicPolicy,
    SubmissionManagementAccessPolicy managementPolicy,
    ICurrentUserAuthorizationService authorizationService,
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
            factory: async ct => await MergeAndWrapAsync(context, ct),
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

    private async Task<Cached<SubmissionAccessData>> MergeAndWrapAsync(
        SubmissionAccessContext context,
        CancellationToken cancellationToken)
    {
        var publicResult = await publicPolicy.GetAccessData(context, cancellationToken);
        var publicData = publicResult.IsSuccess
            ? publicResult.Value.Data
            : new SubmissionAccessData
            {
                FormId = context.FormId.ToString(),
                SubmissionId = context.SubmissionId?.ToString()
            };

        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess || identityResult.Value.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return new Cached<SubmissionAccessData>(
                publicData,
                TimeSpan.FromMinutes(CACHE_MINUTES),
                Guid.NewGuid().ToString("N")
            );
        }

        var managementResult = await managementPolicy.GetAccessData(context, cancellationToken);
        if (!managementResult.IsSuccess)
        {
            return new Cached<SubmissionAccessData>(
                publicData,
                TimeSpan.FromMinutes(CACHE_MINUTES),
                Guid.NewGuid().ToString("N")
            );
        }

        var managementData = managementResult.Value.Data;
        var mergedForm = new HashSet<string>(publicData.FormPermissions);
        mergedForm.UnionWith(managementData.FormPermissions);
        var mergedSub = new HashSet<string>(publicData.SubmissionPermissions);
        mergedSub.UnionWith(managementData.SubmissionPermissions);

        var merged = new SubmissionAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = context.SubmissionId?.ToString(),
            FormPermissions = mergedForm,
            SubmissionPermissions = mergedSub
        };

        return new Cached<SubmissionAccessData>(
            merged,
            TimeSpan.FromMinutes(CACHE_MINUTES),
            Guid.NewGuid().ToString("N")
        );
    }
}
