using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Single policy for public form/submission access control.
/// Uses a two-tiered caching strategy and internal routing to separate cache orchestration from business logic.
/// </summary>
public sealed class PublicFormAccessPolicy(
    IRepository<Form> formRepository,
    ISubmissionTokenService tokenService,
    ISubmissionAccessTokenService accessTokenService,
    ICurrentUserAuthorizationService authorizationService,
    IDateTimeProvider dateTimeProvider,
    HybridCache cache
) : IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext>
{
    private static readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan _formMetadataTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan _safetyMargin = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public async Task<Result<Cached<PublicFormAccessData>>> GetAccessData(
        PublicFormAccessContext context,
        CancellationToken cancellationToken)
    {
        var routeResult = await DetermineRouteAsync(context, cancellationToken);
        if (!routeResult.IsSuccess)
        {
            return routeResult.ToErrorResult<Cached<PublicFormAccessData>>();
        }

        var route = routeResult.Value;

        return await cache.GetOrCreateCachedResultAsync(
            route.CacheKey,
            ct => ExecuteRouteAsync(context, route, ct),
            route.Ttl,
            dateTimeProvider.Now.UtcDateTime,
            tags: ["permissions", $"form:{context.FormId}"],
            cancellationToken: cancellationToken);
    }

    private async Task<Result<PolicyRoute>> DetermineRouteAsync(PublicFormAccessContext context, CancellationToken cancellationToken)
    {
        var token = context.Token;
        var tokenType = context.TokenType;

        if (!string.IsNullOrWhiteSpace(token) && tokenType is not null)
        {
            return tokenType switch
            {
                SubmissionTokenType.AccessToken => ResolveAccessTokenRoute(token),
                SubmissionTokenType.SubmissionToken =>
                    await ResolveSubmissionTokenRouteAsync(token, context.FormId, cancellationToken),
                _ => Result.Error("Unknown token type")
            };
        }

        var isFormPublicResult = await IsFormPublicAsync(context.FormId, cancellationToken);
        if (!isFormPublicResult.IsSuccess)
        {
            return isFormPublicResult.ToErrorResult<PolicyRoute>();
        }

        if (isFormPublicResult.Value)
        {
            return Result.Success(
                new PolicyRoute($"ac:form:public:{context.FormId}", _defaultTtl, RouteType.PublicForm));
        }

        var authDataResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!authDataResult.IsSuccess)
        {
            return authDataResult.ToErrorResult<PolicyRoute>();
        }

        var authData = authDataResult.Value;
        var authTtl = ComputeAuthTtl(authData);

        return Result.Success(
            new PolicyRoute(
                $"ac:form:{context.FormId}:user:{authData.UserId}",
                authTtl,
                RouteType.PrivateForm,
                AuthData: authData));
    }

    private async Task<Result<PolicyRoute>> ResolveSubmissionTokenRouteAsync(
        string token,
        long formId,
        CancellationToken cancellationToken)
    {
        var isFormPublicResult = await IsFormPublicAsync(formId, cancellationToken);
        if (!isFormPublicResult.IsSuccess)
        {
            return isFormPublicResult.ToErrorResult<PolicyRoute>();
        }

        // Submission-token access is scoped to the specific submission (via token),
        // but the cache expiration should follow the user's authorization expiration window.
        var authDataResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!authDataResult.IsSuccess)
        {
            // Fallback: we can't safely align cache lifetime with auth expiration.
            return Result.Success(
                new PolicyRoute($"ac:form:token:{token}", _defaultTtl, RouteType.SubmissionToken));
        }

        var authData = authDataResult.Value;
        var authTtl = ComputeAuthTtl(authData);

        // Include user id so the auth-based TTL does not incorrectly govern other users.
        return Result.Success(
            new PolicyRoute(
                $"ac:form:token:{authData.UserId}:{token}",
                authTtl,
                RouteType.SubmissionToken));
    }

    private async Task<Result<bool>> IsFormPublicAsync(long formId, CancellationToken cancellationToken)
    {
        var cacheKey = $"meta:form:is_public:{formId}";

        return await cache.GetOrCreateResultAsync(
            cacheKey,
            ct => IsFormPublicFromDbAsync(formId, ct),
            new HybridCacheEntryOptions { Expiration = _formMetadataTtl },
            tags: [$"form:{formId}"],
            cancellationToken: cancellationToken);
    }

    private Result<PolicyRoute> ResolveAccessTokenRoute(string token)
    {
        var accessResult = accessTokenService.ValidateAccessToken(token);
        if (!accessResult.IsSuccess)
        {
            return Result.Unauthorized("Invalid access token");
        }

        var claims = accessResult.Value;
        var dynamicTtl = TimeSpan.FromSeconds(Math.Max(1, (claims.ExpiresAt - DateTime.UtcNow - _safetyMargin).TotalSeconds));

        return Result.Success(new PolicyRoute($"ac:form:token:{token}", dynamicTtl, RouteType.AccessToken, Claims: claims));
    }

    private async Task<Result<PublicFormAccessData>> ExecuteRouteAsync(PublicFormAccessContext context, PolicyRoute route, CancellationToken ct)
    {
        return route.Type switch
        {
            RouteType.AccessToken => Result.Success(BuildAccessTokenData(context.FormId, route.Claims!)),
            RouteType.SubmissionToken => await ComputeSubmissionTokenLogicAsync(context, ct),
            RouteType.PublicForm => Result.Success(BuildFormAccessData(context.FormId, isPublic: true)),
            RouteType.PrivateForm => ComputePrivateFormLogic(context, route.AuthData!),
            _ => Result.Error("Unknown route type")
        };
    }

    private async Task<Result<PublicFormAccessData>> ComputeSubmissionTokenLogicAsync(PublicFormAccessContext context, CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.ResolveTokenAsync(context.Token!, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return Result.Unauthorized("Invalid or expired submission token");
        }

        return Result.Success(new PublicFormAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = tokenResult.Value.ToString(),
            FormPermissions = [],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.ReviewSubmission]
        });
    }

    private Result<PublicFormAccessData> ComputePrivateFormLogic(PublicFormAccessContext context, AuthorizationData authData)
    {
        if (authData.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unauthorized("You must be authenticated to access this form");
        }

        if (!authData.IsAdmin && !authData.Permissions.Contains(Actions.Access.PrivateForms))
        {
            return Result.Forbidden("You are not allowed to access this form");
        }

        return Result.Success(BuildFormAccessData(context.FormId, isPublic: false));
    }

    private static PublicFormAccessData BuildFormAccessData(long formId, bool isPublic)
    {
        var formPermissions = new HashSet<string> { ResourcePermissions.Form.View };
        if (!isPublic)
        {
            formPermissions.Add(ResourcePermissions.Form.ViewFiles);
        }

        return new PublicFormAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = null,
            FormPermissions = formPermissions,
            SubmissionPermissions = [ResourcePermissions.Submission.Create, ResourcePermissions.Submission.UploadFile]
        };
    }

    private static PublicFormAccessData BuildAccessTokenData(long formId, SubmissionAccessTokenClaims claims)
    {
        var submissionPermissions = new HashSet<string>();

        if (claims.Permissions.Contains(SubmissionAccessTokenPermissions.View.Name))
        {
            submissionPermissions.UnionWith(ResourcePermissions.Submission.Sets.ViewOnly);
        }

        if (claims.Permissions.Contains(SubmissionAccessTokenPermissions.Edit.Name))
        {
            submissionPermissions.UnionWith(ResourcePermissions.Submission.Sets.EditSubmission);
        }

        if (claims.Permissions.Contains(SubmissionAccessTokenPermissions.Export.Name))
        {
            submissionPermissions.Add(ResourcePermissions.Submission.Export);
        }

        return new PublicFormAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = claims.SubmissionId.ToString(),
            FormPermissions = [],
            SubmissionPermissions = submissionPermissions
        };
    }

    private async Task<Result<bool>> IsFormPublicFromDbAsync(long formId, CancellationToken cancellationtoken)
    {
        var spec = new FormSpecifications.ById(formId).WithProjectionOf(new FormProjections.IsPublicDtoSpec());
        var formDto = await formRepository.FirstOrDefaultAsync(spec, cancellationtoken);

        return formDto is not null ? Result.Success(formDto.IsPublic) : Result.NotFound("Form not found");
    }

    private TimeSpan ComputeAuthTtl(AuthorizationData authData)
    {
        var now = dateTimeProvider.Now.UtcDateTime;
        var authBasedTtl = authData.ExpiresAt - now - _safetyMargin;
        if (authBasedTtl <= TimeSpan.Zero)
        {
            return _defaultTtl;
        }

        return authBasedTtl;
    }

    private enum RouteType { PublicForm, PrivateForm, AccessToken, SubmissionToken }
    private record PolicyRoute(string CacheKey, TimeSpan Ttl, RouteType Type, AuthorizationData? AuthData = null, SubmissionAccessTokenClaims? Claims = null);
}