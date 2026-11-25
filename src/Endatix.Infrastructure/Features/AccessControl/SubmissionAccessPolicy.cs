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
    IDateTimeProvider dateTimeProvider, AccountStanding standing
) : IResourceAccessQuery<SubmissionAccessData, SubmissionAccessContext>
{
    public SubmissionAccessPolicy(ICurrentUserAuthorizationService authorizationService, IRepository<Submission> submissionRepository,
        HybridCache cache, IDateTimeProvider dateTimeProvider)
        : this(authorizationService, submissionRepository, cache, dateTimeProvider, new AccountStanding()) { }

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
        var spec = new SubmissionByFormIdAndSubmissionIdSpec(context.FormId, context.SubmissionId);
        spec.Query.Where(s => s.TenantId == authData.TenantId && s.Form.TenantId == authData.TenantId);
        if (!await submissionRepository.AnyAsync(spec, cancellationToken))
        {
            return Result.NotFound("Submission not found");
        }
        var access = AuthorizedAccess(authData);
        if (!access.IsSuccess)
        {
            return access.ToErrorResult<ICachedData<SubmissionAccessData>>();
        }
        return standing.Refusal<ICachedData<SubmissionAccessData>>(authData)
            ?? await ReadAsync(context, (authData, access.Value), cancellationToken);
    }

    private async Task<Result<ICachedData<SubmissionAccessData>>> ReadAsync(SubmissionAccessContext context,
        (AuthorizationData Auth, AccessLevel Level) access, CancellationToken ct)
    {
        var now = dateTimeProvider.Now.UtcDateTime;
        var ttl = access.Auth.ComputeAuthTtl(now);
        var cacheKey = $"auth:sb_mgmt:form:{context.FormId}:sub:{context.SubmissionId}:tenant:{access.Auth.TenantId}:user:{access.Auth.UserId}:{access.Level}";
        var cached = await cache.GetOrCreateCachedResultAsync<SubmissionAccessData>(cacheKey,
            _ => Task.FromResult(Result.Success(access.Level == AccessLevel.Edit
                ? SubmissionAccessData.CreateWithEditAccess(context.FormId, context.SubmissionId)
                : SubmissionAccessData.CreateWithViewAccess(context.FormId, context.SubmissionId))),
            ttl, now, tags: ["permissions", $"submission:{context.SubmissionId}"], cancellationToken: ct);
        return cached;
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
