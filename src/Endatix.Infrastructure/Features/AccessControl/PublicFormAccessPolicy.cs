using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.AspNetCore.Http;
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
    HybridCache cache,
    ISubmitterResolver submitterResolver,
    IHttpContextAccessor httpContextAccessor
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
        if (accessRoute.Type == RouteType.SingleResponse)
        {
            var accessDataResult = await ExecuteAccessRouteAsync(context, accessRoute, cancellationToken);
            if (!accessDataResult.IsSuccess)
            {
                return accessDataResult.ToErrorResult<ICachedData<PublicFormAccessData>>();
            }

            return Result.Success(Cached<PublicFormAccessData>.Create(
                accessDataResult.Value,
                dateTimeProvider.Now.UtcDateTime,
                _immediateTtl));
        }

        return await cache.GetOrCreateCachedResultAsync(
            accessRoute.CacheKey,
            ct => ExecuteAccessRouteAsync(context, accessRoute, ct),
            accessRoute.Ttl,
            dateTimeProvider.Now.UtcDateTime,
            tags: ["permissions", .. FormAccessCacheTags.ForFormAndAccess(context.FormId)],
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

        var routingMetadataResult = await GetFormAccessRoutingMetadataAsync(context.FormId, cancellationToken);
        if (!routingMetadataResult.IsSuccess)
        {
            return routingMetadataResult.ToErrorResult<AccessPolicyRoute>();
        }

        var routingMetadata = routingMetadataResult.Value;
        var isPublic = routingMetadata.IsPublic;
        var hasSubmissionToken = hasToken && context.TokenType == SubmissionTokenType.SubmissionToken;

        if (isPublic)
        {
            if (hasSubmissionToken)
            {
                return ResolveSubmissionTokenRoute(context.FormId, context.Token!, isPublic: true, authData: null);
            }

            return ResolveFormOnlyRoute(context.FormId, isPublic: true);
        }

        var authDataResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!authDataResult.IsSuccess)
        {
            return authDataResult.ToErrorResult<AccessPolicyRoute>();
        }

        var authData = authDataResult.Value;
        return hasSubmissionToken
        ? ResolveSubmissionTokenRoute(context.FormId, context.Token!, isPublic: false, authData: authData)
        : ResolveFormOnlyRoute(
            context.FormId,
            isPublic: false,
            authData: authData,
            limitOnePerUser: routingMetadata.LimitOnePerUser);
    }

    private static Result<AccessPolicyRoute> ResolveFormOnlyRoute(
        long formId,
        bool isPublic,
        AuthorizationData? authData = null,
        bool limitOnePerUser = false)
    {
        if (isPublic)
        {
            return Result.Success(new AccessPolicyRoute($"ac:form:public:{formId}", _defaultTtl, RouteType.PublicForm, IsPublic: true));
        }

        if (authData is null)
        {
            return Result.Unauthorized(UNAUTHORIZED_MESSAGE);
        }

        if (limitOnePerUser)
        {
            return Result.Success(
                new AccessPolicyRoute(
                    $"ac:form:{formId}:single_response:user:{authData.UserId}",
                    _immediateTtl,
                    RouteType.SingleResponse,
                    IsPublic: false,
                    AuthData: authData));
        }

        return Result.Success(
            new AccessPolicyRoute(
                $"ac:form:{formId}:user:{authData.UserId}",
                _immediateTtl,
                RouteType.PrivateForm,
                IsPublic: false,
                AuthData: authData));
    }

    private Result<AccessPolicyRoute> ResolveSubmissionTokenRoute(long formId, string token, bool isPublic, AuthorizationData? authData = null) => Result.Success(new AccessPolicyRoute(
                $"ac:sub_token:form:{formId}:token:{token}:userId:{authData?.UserId ?? AuthorizationData.ANONYMOUS_USER_ID}",
                ComputeAuthTtl(authData, dateTimeProvider.Now.UtcDateTime),
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
        RouteType.SingleResponse => await BuildSingleResponseAccessData(context.FormId, route.IsPublic, route.AuthData!, ct),
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

        return Result.Success(CreatePrivateFormAccessData(formId, authData!));
    }

    /// <summary>
    /// Builds the access data for a single response.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="isPublic">Whether the form is public.</param>
    /// <param name="authData">The authorization data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The access data.</returns>
    private async Task<Result<PublicFormAccessData>> BuildSingleResponseAccessData(
        long formId,
        bool isPublic,
        AuthorizationData? authData,
        CancellationToken cancellationToken)
    {
        var canAccessFormResult = CanAccessForm(isPublic, authData);
        if (!canAccessFormResult.IsSuccess)
        {
            return canAccessFormResult.ToErrorResult<PublicFormAccessData>();
        }

        var canCreateSubmission = isPublic || HasPermission(authData, Actions.Submissions.Create);
        var isRespondentTestMode = canCreateSubmission && HasPermission(authData, Actions.Forms.Test);
        var hasUserSubmitted = false;
        if (canCreateSubmission && !isRespondentTestMode)
        {
            var form = await formRepository.FirstOrDefaultAsync(
                new FormSpecifications.ByIdWithRelatedForPublicAccess(formId),
                cancellationToken);

            if (form is null)
            {
                return Result.NotFound("Form not found");
            }

            // Access checks must not upsert Submitters on view-only visits.
            var submitterResolution = await submitterResolver.FindExistingAsync(
                new SubmitterResolveContext(
                    form.TenantId,
                    httpContextAccessor.HttpContext?.User),
                cancellationToken);

            if (submitterResolution.SubmitterId is not null)
            {
                hasUserSubmitted = await submissionRepository.AnyAsync(
                    new SubmissionByFormIdAndSubmitterIdSpec(formId, submitterResolution.SubmitterId.Value),
                    cancellationToken);
            }
        }

        var options = new PublicFormAccessOptions(
            LimitOnePerUser: true,
            HasUserSubmitted: hasUserSubmitted,
            IsRespondentTestMode: isRespondentTestMode);

        var accessData = isPublic
            ? PublicFormAccessData.CreatePublicForm(formId, options)
            : CreatePrivateFormAccessData(formId, authData!, options);

        return Result.Success(accessData);
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

    private async Task<Result<FormProjections.FormAccessRoutingDto>> GetFormAccessRoutingMetadataAsync(long formId, CancellationToken cancellationToken) => await cache.GetOrCreateResultAsync(
            $"meta:form:access_routing:{formId}",
            ct => GetFormAccessRoutingMetadataFromDbAsync(formId, ct),
            new HybridCacheEntryOptions { Expiration = _formMetadataTtl },
            tags: FormAccessCacheTags.ForFormAndAccess(formId),
            cancellationToken: cancellationToken);

    private async Task<Result<FormProjections.FormAccessRoutingDto>> GetFormAccessRoutingMetadataFromDbAsync(long formId, CancellationToken cancellationToken)
    {
        var spec = new FormProjections.AccessRoutingDtoSpec(formId);
        var formDto = await formRepository.FirstOrDefaultAsync(spec, cancellationToken);

        return formDto is not null ? Result.Success(formDto) : Result.NotFound("Form not found");
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

        if (!HasPermission(authData, Actions.Access.PrivateForms))
        {
            return Result.Forbidden(FORBIDDEN_MESSAGE);
        }

        return Result.Success(true);
    }

    private static TimeSpan ComputeAuthTtl(AuthorizationData? authData, DateTime utcNow)
    {
        if (authData is null)
        {
            return _defaultTtl;
        }

        var authDataSafeTtl = authData.ExpiresAt - utcNow;
        return TimeSpan.FromSeconds(Math.Max(_immediateTtl.TotalSeconds, authDataSafeTtl.TotalSeconds));
    }

    private static bool HasPermission(AuthorizationData? authData, string permission)
    {
        if (authData is null)
        {
            return false;
        }

        if (authData.IsAdmin)
        {
            return true;
        }

        return authData.Permissions.Contains(permission);
    }

    private static bool IsAuthenticatedUser(AuthorizationData authData) =>
        authData.UserId != AuthorizationData.ANONYMOUS_USER_ID;

    private static PublicFormAccessData CreatePrivateFormAccessData(
        long formId,
        AuthorizationData authData,
        PublicFormAccessOptions? options = null)
    {
        return HasPermission(authData, Actions.Submissions.Create)
            ? PublicFormAccessData.CreatePublicForm(formId, options)
            : PublicFormAccessData.CreateViewOnlyForm(formId, options);
    }

    private enum RouteType { PublicForm, PrivateForm, SingleResponse, AccessToken, SubmissionToken, FormAccessToken }
    private sealed record AccessPolicyRoute(
        string CacheKey,
        TimeSpan Ttl,
        RouteType Type,
        bool IsPublic = false,
        AuthorizationData? AuthData = null,
        SubmissionAccessTokenClaims? Claims = null,
        FormAccessTokenClaims? FormTokenClaims = null);
}
