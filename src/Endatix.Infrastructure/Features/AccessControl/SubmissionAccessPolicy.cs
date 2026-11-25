using Ardalis.Specification;
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

        return await ResolveAsync(context, identityResult.Value, cancellationToken);
    }

    private async Task<Result<ICachedData<SubmissionAccessData>>> ResolveAsync(
        SubmissionAccessContext context, AuthorizationData authData, CancellationToken cancellationToken)
    {
        if (!await IsVisibleAsync(context, authData, cancellationToken))
        {
            return Result.NotFound("Submission not found");
        }
        var access = AuthorizedAccess(authData);
        if (!access.IsSuccess)
        {
            return access.ToErrorResult<ICachedData<SubmissionAccessData>>();
        }
        var authorized = new Grant(context, authData, access.Value);
        return await ReadAsync(authorized, cancellationToken);
    }

    private Task<bool> IsVisibleAsync(SubmissionAccessContext context, AuthorizationData auth, CancellationToken ct)
    {
        var spec = new SubmissionByFormIdAndSubmissionIdSpec(context.FormId, context.SubmissionId);
        spec.Query.Where(s => s.TenantId == auth.TenantId && s.Form.TenantId == auth.TenantId);
        return submissionRepository.AnyAsync(spec, ct);
    }

    private async Task<Result<ICachedData<SubmissionAccessData>>> ReadAsync(Grant grant, CancellationToken ct)
    {
        var now = dateTimeProvider.Now.UtcDateTime;
        var ttl = grant.Auth.ComputeAuthTtl(now);
        var cached = await cache.GetOrCreateAsync(grant.Key,
            _ => ValueTask.FromResult(new Cached<SubmissionAccessData>(grant.Permissions(), now, ttl)),
            new HybridCacheEntryOptions { Expiration = ttl },
            tags: ["permissions", FormAccessCacheTags.ForForm(grant.Target.FormId), $"submission:{grant.Target.SubmissionId}"], cancellationToken: ct);
        var deadline = grant.Auth.ExpiresAt;
        return Result.Success<ICachedData<SubmissionAccessData>>(deadline != default && deadline < cached.ExpiresAt
            ? cached with { ExpiresAt = deadline } : cached);
    }

    private sealed record Grant(SubmissionAccessContext Target, AuthorizationData Auth, AccessLevel Level)
    {
        internal string Key => $"auth:sb_mgmt:form:{Target.FormId}:sub:{Target.SubmissionId}:tenant:{Auth.TenantId}:user:{Auth.UserId}:{Level}";
        internal SubmissionAccessData Permissions() => Level == AccessLevel.Edit
            ? SubmissionAccessData.CreateWithEditAccess(Target.FormId, Target.SubmissionId)
            : SubmissionAccessData.CreateWithViewAccess(Target.FormId, Target.SubmissionId);
    }

    private static Result<AccessLevel> AuthorizedAccess(AuthorizationData authData)
    {
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
            return AccessLevel.Edit;
        }
        return authData.HasPermission(Actions.Submissions.View) ? Result.Success(AccessLevel.View)
            : Result.Forbidden("You are not authorized to access this submission.");
    }
}
