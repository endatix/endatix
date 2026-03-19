using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Caching;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
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
    private static readonly TimeSpan _tokenTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan _formMetadataTtl = TimeSpan.FromHours(15);
    private static readonly TimeSpan _safetyMargin = TimeSpan.FromSeconds(10);

    internal static class CacheKeyGenerator
    {
        /// <summary>
        /// Generates a cache key for public form access data by token.
        /// </summary>
        /// <param name="token">The token to generate a cache key for.</param>
        /// <returns>The cache key for the token.</returns>
        internal static string ByToken(string token) => $"ac:form:token:{token}";

        internal static string ForPrivateForm(long formId, string userId) => $"ac:form:{formId}:user:{userId}";

        internal static string ForPublicForm(long formId) => $"ac:form:public:{formId}";
    }


    /// <summary>
    /// The access policy route for resolving the access data.
    /// </summary>
    internal enum AccessPolicyRoute { ForPublicForm, ForPrivateForm, ByAccessToken, BySubmissionToken }

    /// <summary>
    /// The cache instruction for the access data.
    /// Allows for dynamic cache keys, expiration that can adapt to the access context
    /// </summary>
    internal sealed record CacheInstruction(string Key, TimeSpan Expiration, AccessPolicyRoute Route, AuthorizationData? AuthData = null)
    {
        internal static CacheInstruction ByAccessToken(string token, TimeSpan? ttl) => new(CacheKeyGenerator.ByToken(token), ttl ?? _defaultTtl, AccessPolicyRoute.ByAccessToken);

        internal static CacheInstruction BySubmissionToken(string token, TimeSpan? ttl) => new(CacheKeyGenerator.ByToken(token), ttl ?? _tokenTtl, AccessPolicyRoute.BySubmissionToken);

        internal static CacheInstruction ForPublicForm(long formId) => new(CacheKeyGenerator.ForPublicForm(formId), _defaultTtl, AccessPolicyRoute.ForPublicForm);

        internal static CacheInstruction ForPrivateForm(long formId, string userId, AuthorizationData authData) => new(
                    CacheKeyGenerator.ForPrivateForm(formId, userId),
                    _defaultTtl,
                    AccessPolicyRoute.ForPrivateForm,
                    authData);
    }

    /// <inheritdoc/>
    public async Task<Result<Cached<PublicFormAccessData>>> GetAccessData(
        PublicFormAccessContext context,
        CancellationToken cancellationToken)
    {
        var instructionResult = await ResolvePolicyRouteAsync(context, cancellationToken);
        if (!instructionResult.IsSuccess)
        {
            return instructionResult.ToErrorResult<Cached<PublicFormAccessData>>();
        }

        var instruction = instructionResult.Value!;

        try
        {
            var cachedEnvelope = await cache.GetOrCreateAsync(
                key: instruction.Key,
                factory: async ct => await ExecutePureLogicAsync(context, instruction, ct),
                options: new HybridCacheEntryOptions { Expiration = instruction.Expiration },
                tags: ["permissions", $"form:{context.FormId}"],
                cancellationToken: cancellationToken
            );

            return Result<Cached<PublicFormAccessData>>.Success(cachedEnvelope);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<Cached<PublicFormAccessData>>.Error(ex.Message);
        }
    }

    /// <summary>
    /// Resolves the policy route for the access data.
    /// </summary>
    internal async Task<Result<CacheInstruction>> ResolvePolicyRouteAsync(PublicFormAccessContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(context.Token))
        {
            return context.TokenType switch
            {
                SubmissionTokenType.AccessToken =>
                    Result.Success(CacheInstruction.ByAccessToken(context.Token, _defaultTtl)),
                SubmissionTokenType.SubmissionToken =>
                    Result.Success(CacheInstruction.BySubmissionToken(context.Token, _tokenTtl)),
                _ => Result.Error("Unknown token type")
            };
        }

        var isFormPublic = await cache.GetOrCreateAsync(
            $"meta:form:is_public:{context.FormId}",
            async ct => await IsFormPublicFromDbAsync(context.FormId, ct),
            new HybridCacheEntryOptions { Expiration = _formMetadataTtl },
            tags: [$"form:{context.FormId}"],
            cancellationToken: cancellationToken
        );

        if (isFormPublic)
        {
            return Result.Success(CacheInstruction.ForPublicForm(context.FormId));
        }

        var authDataResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!authDataResult.IsSuccess)
        {
            return Result.Error("Failed to get authorization data");
        }

        var authData = authDataResult.Value;

        return Result.Success(CacheInstruction.ForPrivateForm(context.FormId, authData.UserId, authData));
    }

    private async ValueTask<Cached<PublicFormAccessData>> ExecutePureLogicAsync(
        PublicFormAccessContext context,
        CacheInstruction instruction,
        CancellationToken ct)
    {
        // Dispatch to pure logic based on the enum
        var dataResult = instruction.Route switch
        {
            AccessPolicyRoute.ByAccessToken => ComputeAccessTokenLogic(context, out instruction),
            AccessPolicyRoute.BySubmissionToken => await ComputeSubmissionTokenLogicAsync(context, ct),
            AccessPolicyRoute.ForPublicForm => ComputePublicFormLogic(context),
            AccessPolicyRoute.ForPrivateForm => ComputePrivateFormLogic(context, instruction.AuthData),
            _ => throw new InvalidOperationException("Unknown route")
        };

        if (!dataResult.IsSuccess)
        {
            throw new UnauthorizedAccessException(dataResult.Errors.FirstOrDefault());
        }

        return Cached<PublicFormAccessData>.Create(dataResult.Value, dateTimeProvider.Now.UtcDateTime, instruction.Expiration);
    }

    private Result<PublicFormAccessData> ComputeAccessTokenLogic(PublicFormAccessContext context, out CacheInstruction updatedInstruction)
    {
        updatedInstruction = default!;
        var accessResult = accessTokenService.ValidateAccessToken(context.Token!);
        if (!accessResult.IsSuccess)
        {
            return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.InvalidToken);
        }

        var claims = accessResult.Value!;
        var dynamicTtl = TimeSpan.FromSeconds(Math.Max(1, (claims.ExpiresAt - DateTime.UtcNow - _safetyMargin).TotalSeconds));

        // Update the instruction so the envelope gets the correct dynamic TTL
        updatedInstruction = CacheInstruction.ByAccessToken(context.Token!, dynamicTtl);

        return Result<PublicFormAccessData>.Success(BuildAccessTokenData(context.FormId, claims));
    }

    private async Task<Result<PublicFormAccessData>> ComputeSubmissionTokenLogicAsync(PublicFormAccessContext context, CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.ResolveTokenAsync(context.Token!, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return Result.Invalid(SubmissonTokenErrors.ValidationErrors.SubmissionTokenInvalid);
        }

        return Result.Success(new PublicFormAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = tokenResult.Value.ToString(),
            FormPermissions = [],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.ReviewSubmission]
        });
    }

    private Result<PublicFormAccessData> ComputePublicFormLogic(PublicFormAccessContext context)
    {
        return Result.Success(new PublicFormAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = null,
            FormPermissions = [ResourcePermissions.Form.View],
            SubmissionPermissions = [ResourcePermissions.Submission.Create, ResourcePermissions.Submission.UploadFile]
        });
    }

    private Result<PublicFormAccessData> ComputePrivateFormLogic(PublicFormAccessContext context, AuthorizationData? authData)
    {
        if (authData is null)
        {
            return Result.Error("Authorization data is missing");
        }

        if (authData.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unauthorized("You must be authenticated to access this form");
        }

        var canAccessPrivateForm = authData.IsAdmin || authData.Permissions.Contains(Actions.Access.PrivateForms);
        if (!canAccessPrivateForm)
        {
            return Result.Forbidden("You are not allowed to access this form");
        }

        return Result.Success(new PublicFormAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = null,
            FormPermissions = [ResourcePermissions.Form.View],
            SubmissionPermissions = [ResourcePermissions.Submission.Create, ResourcePermissions.Submission.UploadFile]
        });
    }


    private async Task<bool> IsFormPublicFromDbAsync(long formId, CancellationToken cancellationtoken)
    {
        var publicDtoSpec = new FormProjections.IsPublicDtoSpec();
        var spec = new FormSpecifications.ById(formId).WithProjectionOf(publicDtoSpec);
        var formDto = await formRepository.FirstOrDefaultAsync(spec, cancellationtoken);

        return formDto?.IsPublic ?? false;
    }

    private static PublicFormAccessData BuildAccessTokenData(long formId, SubmissionAccessTokenClaims claims)
    {
        var submissionPermissions = new HashSet<string>();
        if (claims.Permissions.Contains(SubmissionAccessTokenPermissions.View.Name, StringComparer.OrdinalIgnoreCase))
        {
            submissionPermissions.Add(ResourcePermissions.Submission.View);
            submissionPermissions.Add(ResourcePermissions.Submission.ViewFiles);
        }

        if (claims.Permissions.Contains(SubmissionAccessTokenPermissions.Edit.Name, StringComparer.OrdinalIgnoreCase))
        {
            submissionPermissions.UnionWith(ResourcePermissions.Submission.Sets.EditSubmission);
        }

        if (claims.Permissions.Contains(SubmissionAccessTokenPermissions.Export.Name, StringComparer.OrdinalIgnoreCase))
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
}