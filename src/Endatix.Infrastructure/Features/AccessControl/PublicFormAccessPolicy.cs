using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Single policy for public form/submission access control.
/// Uses a two-tiered caching strategy and internal routing to separate cache orchestration from business logic.
/// </summary>
public sealed class PublicFormAccessPolicy(
    IRepository<Form> formRepository,
    IRepository<Submission> submissionRepository,
    ISubmissionTokenService tokenService,
    ISubmissionAccessTokenService accessTokenService,
    IFormAccessTokenService formAccessTokenService,
    ICurrentUserAuthorizationService authorizationService,
    IDateTimeProvider dateTimeProvider,
    IOptions<EndatixJwtOptions> endatixJwtOptions,
    HybridCache cache
) : IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext>
{
    private static readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _formMetadataTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan _immediateTtl = TimeSpan.FromSeconds(1);

    private const string UNAUTHORIZED_MESSAGE = "You must be authenticated to access this form";
    private const string FORBIDDEN_MESSAGE = "You are not allowed to access this form";

    /// <inheritdoc/>
    public async Task<Result<ICachedData<PublicFormAccessData>>> GetAccessData(
        PublicFormAccessContext context,
        CancellationToken cancellationToken)
    {
        var routeResult = await DetermineAccessRouteAsync(context, cancellationToken);
        if (!routeResult.IsSuccess)
        {
            return routeResult.ToErrorResult<ICachedData<PublicFormAccessData>>();
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
        if (hasToken
            && context.TokenType is not SubmissionTokenType.AccessToken
            && context.TokenType is not SubmissionTokenType.SubmissionToken
            && context.TokenType is not SubmissionTokenType.FormToken)
        {
            return Result<AccessPolicyRoute>.Error("Unknown token type");
        }
        if (hasToken && context.TokenType == SubmissionTokenType.FormToken)
        {
            return await ResolveFormAccessTokenRouteAsync(context, cancellationToken);
        }
        if (hasToken && context.TokenType == SubmissionTokenType.AccessToken)
        {
            return ResolveAccessTokenRoute(context.FormId, context.Token!);
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
            ? ResolveSubmissionTokenRoute(context.FormId, context.Token!, isPublic: true, authData: null)
            : ResolveFormOnlyRoute(context.FormId, isPublic: true);
        }

        var authDataResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!authDataResult.IsSuccess)
        {
            return authDataResult.ToErrorResult<AccessPolicyRoute>();
        }

        var authData = authDataResult.Value;
        return hasSubmissionToken
        ? ResolveSubmissionTokenRoute(context.FormId, context.Token!, isPublic: false, authData: authData)
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
                authData.ComputeAuthTtl(dateTimeProvider.Now.UtcDateTime),
                RouteType.PrivateForm,
                IsPublic: false,
                AuthData: authData));
    }

    private Result<AccessPolicyRoute> ResolveSubmissionTokenRoute(long formId, string token, bool isPublic, AuthorizationData? authData = null) => Result.Success(new AccessPolicyRoute(
                $"ac:sub_token:form:{formId}:token:{token}:userId:{authData?.UserId ?? AuthorizationData.ANONYMOUS_USER_ID}",
                authData is not null ? authData.ComputeAuthTtl(dateTimeProvider.Now.UtcDateTime) : _defaultTtl,
                RouteType.SubmissionToken,
                IsPublic: isPublic,
                AuthData: authData)
                );

    private Result<AccessPolicyRoute> ResolveAccessTokenRoute(long formId, string token)
    {
        var accessResult = accessTokenService.ValidateAccessToken(token);
        if (!accessResult.IsSuccess)
        {
            return accessResult.ToErrorResult<AccessPolicyRoute>();
        }

        var claims = accessResult.Value;
        var tokenSafeExpirationTtl = (claims.ExpiresAt - dateTimeProvider.Now.UtcDateTime).TotalSeconds;
        var dynamicTtl = TimeSpan.FromSeconds(Math.Max(_immediateTtl.TotalSeconds, tokenSafeExpirationTtl));

        return Result.Success(new AccessPolicyRoute($"ac:form:{formId}:token:{token}", dynamicTtl, RouteType.AccessToken, Claims: claims));
    }

    private async Task<Result<AccessPolicyRoute>> ResolveFormAccessTokenRouteAsync(
        PublicFormAccessContext context,
        CancellationToken cancellationToken)
    {
        var accessResult = formAccessTokenService.ValidateToken(context.Token!);
        if (!accessResult.IsSuccess)
        {
            return accessResult.ToErrorResult<AccessPolicyRoute>();
        }

        var claims = accessResult.Value;
        if (claims.FormId != context.FormId)
        {
            return Result.Forbidden("Form access token does not match this form.");
        }

        var form = await formRepository.FirstOrDefaultAsync(
            new FormSpecifications.ByIdWithRelatedForPublicAccess(claims.FormId),
            cancellationToken);

        if (form is null)
        {
            return Result.NotFound("Form not found");
        }

        if (form.TenantId != claims.TenantId)
        {
            return Result.Forbidden("Form access token tenant does not match this form.");
        }

        var tokenSafeExpirationTtl = (claims.ExpiresAtUtc - dateTimeProvider.Now.UtcDateTime).TotalSeconds;
        var dynamicTtl = TimeSpan.FromSeconds(Math.Max(_immediateTtl.TotalSeconds, tokenSafeExpirationTtl));

        var signingKey = endatixJwtOptions.Value.SigningKey;
        var tokenFingerprint = CacheKeyFingerprint.ComputeHmacSha256Hex(context.Token!, signingKey);

        return Result.Success(new AccessPolicyRoute(
            $"ac:form_token:{context.FormId}:token_fp:{tokenFingerprint}",
            dynamicTtl,
            RouteType.FormAccessToken,
            FormTokenClaims: claims));
    }

    private async Task<Result<PublicFormAccessData>> ExecuteAccessRouteAsync(PublicFormAccessContext context, AccessPolicyRoute route, CancellationToken ct) => route.Type switch
    {
        RouteType.PublicForm => BuildPublicFormAccessData(context.FormId),
        RouteType.PrivateForm => BuildPrivateFormAccessData(context.FormId, route.AuthData!),
        RouteType.AccessToken => await BuildAccessTokenDataAsync(context.FormId, route.Claims!, ct),
        RouteType.SubmissionToken => await BuildSubmissionTokenDataAsync(context, route, ct),
        RouteType.FormAccessToken => BuildPublicFormReBacAccessData(route.FormTokenClaims!),
        _ => Result.Error("Unknown route type")
    };

    private static Result<PublicFormAccessData> BuildPrivateFormAccessData(long formId, AuthorizationData? authData)
    {
        var canAccessFormResult = CanAccessForm(isPublic: false, authData);
        if (!canAccessFormResult.IsSuccess)
        {
            return canAccessFormResult.ToErrorResult<PublicFormAccessData>();
        }

        return BuildPublicFormAccessData(formId);
    }

    private static Result<PublicFormAccessData> BuildPublicFormAccessData(long formId) => Result.Success(PublicFormAccessData.CreatePublicForm(formId));

    private static Result<PublicFormAccessData> BuildPublicFormReBacAccessData(FormAccessTokenClaims claims) =>
        Result.Success(PublicFormAccessData.CreatePublicForm(claims.FormId));

    private async Task<Result<PublicFormAccessData>> BuildAccessTokenDataAsync(
        long formId,
        SubmissionAccessTokenClaims claims,
        CancellationToken cancellationToken)
    {
        var relationResult = await ValidateSubmissionRelationAsync(formId, claims.SubmissionId, cancellationToken);
        if (!relationResult.IsSuccess)
        {
            return relationResult.ToErrorResult<PublicFormAccessData>();
        }

        return Result.Success(PublicFormAccessData.CreateWithAccessTokenClaims(formId, claims));
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

        var relationResult = await ValidateSubmissionRelationAsync(context.FormId, tokenResult.Value, cancellationToken);
        if (!relationResult.IsSuccess)
        {
            return relationResult.ToErrorResult<PublicFormAccessData>();
        }

        return Result.Success(PublicFormAccessData.CreateWithSubmissionToken(context.FormId, tokenResult.Value));
    }

    private async Task<Result<bool>> ValidateSubmissionRelationAsync(
        long formId,
        long submissionId,
        CancellationToken cancellationToken)
    {
        // Token resolve ignores tenant filters; relation check must too — public callers often have no tenant match.
        var spec = new SubmissionByFormIdAndSubmissionIdSpec(formId, submissionId);
        var relationExists = await submissionRepository.AnyAsync(spec, cancellationToken);
        return relationExists
            ? Result.Success(true)
            : Result.NotFound("Submission not found");
    }

    private async Task<Result<bool>> IsFormPublicAsync(long formId, CancellationToken cancellationToken) => await cache.GetOrCreateResultAsync(
            $"meta:form:is_public:{formId}",
            ct => IsFormPublicFromDbAsync(formId, ct),
            new HybridCacheEntryOptions { Expiration = _formMetadataTtl },
            tags: [$"form:{formId}"],
            cancellationToken: cancellationToken);

    private async Task<Result<bool>> IsFormPublicFromDbAsync(long formId, CancellationToken cancellationToken)
    {
        var spec = new FormProjections.IsPublicDtoSpec(formId);
        var formDto = await formRepository.FirstOrDefaultAsync(spec, cancellationToken);

        return formDto is not null ? Result.Success(formDto.IsPublic) : Result.NotFound("Form not found");
    }

    private static Result<bool> CanAccessForm(bool isPublic, AuthorizationData? authData = null)
    {
        if (isPublic)
        {
            return Result.Success(true);
        }

        if (authData is null || authData.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unauthorized(UNAUTHORIZED_MESSAGE);
        }

        if (!authData.HasPermission(Actions.Access.PrivateForms))
        {
            return Result.Forbidden(FORBIDDEN_MESSAGE);
        }

        return Result.Success(true);
    }

    private enum RouteType { PublicForm, PrivateForm, AccessToken, SubmissionToken, FormAccessToken }
    private sealed record AccessPolicyRoute(
        string CacheKey,
        TimeSpan Ttl,
        RouteType Type,
        bool IsPublic = false,
        AuthorizationData? AuthData = null,
        SubmissionAccessTokenClaims? Claims = null,
        FormAccessTokenClaims? FormTokenClaims = null);
}
