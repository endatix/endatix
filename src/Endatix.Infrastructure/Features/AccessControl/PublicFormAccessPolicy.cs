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
    private static readonly TimeSpan _immediateTtl = TimeSpan.FromSeconds(1);

    private const string UNAUTHORIZED_MESSAGE = "You must be authenticated to access this form";
    private const string FORBIDDEN_MESSAGE = "You are not allowed to access this form";

    /// <inheritdoc/>
    public async Task<Result<Cached<PublicFormAccessData>>> GetAccessData(
        PublicFormAccessContext context,
        CancellationToken cancellationToken)
    {
        var routeResult = await DetermineAccessRouteAsync(context, cancellationToken);
        if (!routeResult.IsSuccess)
        {
            return routeResult.ToErrorResult<Cached<PublicFormAccessData>>();
        }

        var accessRoute = routeResult.Value;

        return await cache.GetOrCreateCachedResultAsync(
            accessRoute.CacheKey,
            ct => ExecuteAccessRouteAsync(context, accessRoute, ct),
            accessRoute.Ttl,
            dateTimeProvider.Now.UtcDateTime,
            tags: ["permissions", $"form:{context.FormId}"],
            cancellationToken: cancellationToken);
    }

    private async Task<Result<AccessPolicyRoute>> DetermineAccessRouteAsync(PublicFormAccessContext context, CancellationToken cancellationToken)
    {
        var hasToken = !string.IsNullOrWhiteSpace(context.Token) && context.TokenType is not null;
        if (hasToken && context.TokenType == SubmissionTokenType.AccessToken)
        {
            return ResolveAccessTokenRoute(context.Token!);
        }

        var isFormPublicResult = await IsFormPublicAsync(context.FormId, cancellationToken);
        if (!isFormPublicResult.IsSuccess)
        {
            return isFormPublicResult.ToErrorResult<AccessPolicyRoute>();
        }

        var isPublic = isFormPublicResult.Value;
        var hasSubmissionToken = hasToken && context.TokenType == SubmissionTokenType.SubmissionToken;

        if (isPublic)
        {
            return hasSubmissionToken
            ? ResolveSubmissionTokenRoute(context.Token!, isPublic: true, authData: null)
            : ResolveFormOnlyRoute(context.FormId, isPublic: true);
        }

        var authDataResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!authDataResult.IsSuccess)
        {
            return authDataResult.ToErrorResult<AccessPolicyRoute>();
        }

        var authData = authDataResult.Value;
        return hasSubmissionToken
        ? ResolveSubmissionTokenRoute(context.Token!, isPublic: false, authData: authData)
        : ResolveFormOnlyRoute(context.FormId, isPublic: false, authData: authData);
    }

    private Result<AccessPolicyRoute> ResolveFormOnlyRoute(long formId, bool isPublic, AuthorizationData? authData = null)
    {
        if (isPublic)
        {
            return Result.Success(new AccessPolicyRoute($"ac:form:public:{formId}", _defaultTtl, RouteType.PublicForm, IsPublic: true));
        }

        if (authData is null)
        {
            return Result.Unauthorized(UNAUTHORIZED_MESSAGE);
        }

        return Result.Success(
            new AccessPolicyRoute(
                $"ac:form:{formId}:user:{authData.UserId}",
                ComputeAuthTtl(authData),
                RouteType.PrivateForm,
                IsPublic: false,
                AuthData: authData));
    }

    private Result<AccessPolicyRoute> ResolveSubmissionTokenRoute(string token, bool isPublic, AuthorizationData? authData = null) => Result.Success(new AccessPolicyRoute(
                $"ac:sub_token:{token}:userId:{authData?.UserId ?? AuthorizationData.ANONYMOUS_USER_ID}",
                ComputeAuthTtl(authData),
                RouteType.SubmissionToken,
                IsPublic: isPublic,
                AuthData: authData)
                );

    private Result<AccessPolicyRoute> ResolveAccessTokenRoute(string token)
    {
        var accessResult = accessTokenService.ValidateAccessToken(token);
        if (!accessResult.IsSuccess)
        {
            return accessResult.ToErrorResult<AccessPolicyRoute>();
        }

        var claims = accessResult.Value;
        var tokenSafeExpirationTtl = (claims.ExpiresAt - dateTimeProvider.Now.UtcDateTime).TotalSeconds;
        var dynamicTtl = TimeSpan.FromSeconds(Math.Max(_immediateTtl.TotalSeconds, tokenSafeExpirationTtl));

        return Result.Success(new AccessPolicyRoute($"ac:form:token:{token}", dynamicTtl, RouteType.AccessToken, Claims: claims));
    }

    private async Task<Result<PublicFormAccessData>> ExecuteAccessRouteAsync(PublicFormAccessContext context, AccessPolicyRoute route, CancellationToken ct)
    {
        return route.Type switch
        {
            RouteType.PublicForm => BuildPublicFormAccessData(context.FormId),
            RouteType.PrivateForm => BuildPrivateFormAccessData(context.FormId, route.AuthData!),
            RouteType.AccessToken => Result.Success(BuildAccessTokenData(context.FormId, route.Claims!)),
            RouteType.SubmissionToken => await BuildSubmissionTokenDataAsync(context, route, ct),
            _ => Result.Error("Unknown route type")
        };
    }

    private Result<PublicFormAccessData> BuildPrivateFormAccessData(long formId, AuthorizationData? authData)
    {
        var canAccessFormResult = CanAccessForm(isPublic: false, authData);
        if (!canAccessFormResult.IsSuccess)
        {
            return canAccessFormResult.ToErrorResult<PublicFormAccessData>();
        }

        return BuildPublicFormAccessData(formId);
    }

    private static Result<PublicFormAccessData> BuildPublicFormAccessData(long formId)
    {
        return Result.Success(new PublicFormAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = null,
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.CreateSubmission]
        });
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
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = submissionPermissions
        };
    }

    private async Task<Result<PublicFormAccessData>> BuildSubmissionTokenDataAsync(PublicFormAccessContext context, AccessPolicyRoute route, CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.ResolveTokenAsync(context.Token!, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return tokenResult.ToErrorResult<PublicFormAccessData>();
        }

        var canAccessFormResult = CanAccessForm(route.IsPublic, route.AuthData);
        if (!canAccessFormResult.IsSuccess)
        {
            return canAccessFormResult.ToErrorResult<PublicFormAccessData>();
        }

        return Result.Success(new PublicFormAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = tokenResult.Value.ToString(),
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.FillInSubmission]
        });
    }

    private async Task<Result<bool>> IsFormPublicAsync(long formId, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateResultAsync(
            $"meta:form:is_public:{formId}",
            ct => IsFormPublicFromDbAsync(formId, ct),
            new HybridCacheEntryOptions { Expiration = _formMetadataTtl },
            tags: [$"form:{formId}"],
            cancellationToken: cancellationToken);
    }

    private async Task<Result<bool>> IsFormPublicFromDbAsync(long formId, CancellationToken cancellationtoken)
    {
        var spec = new FormSpecifications.ById(formId).WithProjectionOf(new FormProjections.IsPublicDtoSpec());
        var formDto = await formRepository.FirstOrDefaultAsync(spec, cancellationtoken);

        return formDto is not null ? Result.Success(formDto.IsPublic) : Result.NotFound("Form not found");
    }

    private Result<bool> CanAccessForm(bool isPublic, AuthorizationData? authData = null)
    {
        if (isPublic)
        {
            return Result.Success(true);
        }

        if (authData is null || authData.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unauthorized(UNAUTHORIZED_MESSAGE);
        }

        if (!authData.IsAdmin && !authData.Permissions.Contains(Actions.Access.PrivateForms))
        {
            return Result.Forbidden(FORBIDDEN_MESSAGE);
        }

        return Result.Success(true);
    }

    private TimeSpan ComputeAuthTtl(AuthorizationData? authData)
    {
        if (authData is null)
        {
            return _defaultTtl;
        }

        var authDataSafeTtl = authData.ExpiresAt - dateTimeProvider.Now.UtcDateTime;
        return TimeSpan.FromSeconds(Math.Max(_immediateTtl.TotalSeconds, authDataSafeTtl.TotalSeconds));
    }

    private enum RouteType { PublicForm, PrivateForm, AccessToken, SubmissionToken }
    private record AccessPolicyRoute(string CacheKey, TimeSpan Ttl, RouteType Type, bool IsPublic = false, AuthorizationData? AuthData = null, SubmissionAccessTokenClaims? Claims = null);
}
