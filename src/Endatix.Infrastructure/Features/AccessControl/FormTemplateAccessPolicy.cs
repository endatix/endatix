using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
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
    IDateTimeProvider dateTimeProvider, AccountStanding standing
) : IResourceAccessQuery<FormTemplateAccessData, FormTemplateAccessContext>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public FormTemplateAccessPolicy(ICurrentUserAuthorizationService authorizationService, HybridCache cache, IDateTimeProvider dateTimeProvider)
        : this(authorizationService, cache, dateTimeProvider, new AccountStanding()) { }

    public async Task<Result<ICachedData<FormTemplateAccessData>>> GetAccessData(
        FormTemplateAccessContext context,
        CancellationToken cancellationToken)
    {
        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess)
        {
            return identityResult.ToErrorResult<ICachedData<FormTemplateAccessData>>();
        }

        var authData = identityResult.Value;
        var access = AuthorizedAccess(authData);
        if (!access.IsSuccess)
        {
            return access.ToErrorResult<ICachedData<FormTemplateAccessData>>();
        }
        return standing.Refusal<ICachedData<FormTemplateAccessData>>(authData)
            ?? await ReadAsync(new Grant(context.TemplateId, authData, access.Value), cancellationToken);
    }

    private async Task<Result<ICachedData<FormTemplateAccessData>>> ReadAsync(Grant grant, CancellationToken ct)
    {
        var now = dateTimeProvider.Now.UtcDateTime;
        var ttl = grant.Auth.ComputeAuthTtl(now);
        var entry = await cache.GetOrCreateAsync(grant.Key,
            _ => ValueTask.FromResult(new Cached<FormTemplateAccessData>(grant.Permissions(), now, ttl)),
            new HybridCacheEntryOptions { Expiration = ttl }, tags: ["permissions", $"template:{grant.Id}"], cancellationToken: ct);
        var deadline = grant.Auth.ExpiresAt;
        return Result.Success<ICachedData<FormTemplateAccessData>>(deadline != default && deadline < entry.ExpiresAt
            ? entry with { ExpiresAt = deadline } : entry);
    }

    private sealed record Grant(long Id, AuthorizationData Auth, AccessLevel Level)
    {
        internal string Key => $"auth:tpl_mgmt:{Id}:tenant:{Auth.TenantId}:user:{Auth.UserId}:{Level}";
        internal FormTemplateAccessData Permissions() => Level == AccessLevel.Edit
            ? FormTemplateAccessData.CreateWithEditAccess(Id) : FormTemplateAccessData.CreateWithViewAccess(Id);
    }

    private static Result<AccessLevel> AuthorizedAccess(AuthorizationData authData)
    {
        if (authData is null || authData.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unauthorized("You are not authorized to access this form template.");
        }

        if (!authData.HasPermission(Actions.Access.Hub))
        {
            return Result.Forbidden("You are not authorized to access this form template.");
        }

        if (authData.HasPermission(Actions.Templates.Edit))
        {
            return AccessLevel.Edit;
        }

        return authData.HasPermission(Actions.Templates.View) ? Result.Success(AccessLevel.View)
            : Result.Forbidden("You are not authorized to access this form template.");
    }
}
