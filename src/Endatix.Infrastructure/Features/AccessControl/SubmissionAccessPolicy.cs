using Ardalis.Specification;
using Endatix.Core.Abstractions.Authorization;
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
/// Single policy for public/respondent submission access.
/// Uses a two-tiered caching strategy and internal routing to separate cache orchestration from business logic.
/// </summary>
public sealed class SubmissionAccessPolicy(
    IRepository<Form> formRepository,
    ISubmissionTokenService tokenService,
    ISubmissionAccessTokenService accessTokenService,
    ICurrentUserAuthorizationService authorizationService,
    HybridCache cache
) : IResourceAccessStrategy<SubmissionAccessData, SubmissionAccessContext>
{
    private static readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _tokenTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan _formMetadataTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan _safetyMargin = TimeSpan.FromSeconds(10);

    private enum AccessRoute { AccessToken, SubmissionToken, PublicForm, PrivateForm }

    private record CacheInstruction(string Key, TimeSpan Expiration, AccessRoute Route);

    public async Task<Result<Cached<SubmissionAccessData>>> GetAccessData(
        SubmissionAccessContext context,
        CancellationToken cancellationToken)
    {
        var instructionResult = await DetermineRouteAsync(context, cancellationToken);
        if (!instructionResult.IsSuccess)
        {
            return Result<Cached<SubmissionAccessData>>.Error(string.Join(", ", instructionResult.Errors));
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

            return Result<Cached<SubmissionAccessData>>.Success(cachedEnvelope);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<Cached<SubmissionAccessData>>.Error(ex.Message);
        }
    }

    private async Task<Result<CacheInstruction>> DetermineRouteAsync(SubmissionAccessContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(context.Token))
        {
            return context.TokenType switch
            {
                SubmissionTokenType.AccessToken =>
                    Result<CacheInstruction>.Success(new($"auth:sub:jwt:{context.Token}", _defaultTtl, AccessRoute.AccessToken)),
                SubmissionTokenType.SubmissionToken =>
                    Result<CacheInstruction>.Success(new($"auth:sub:token:{context.Token}", _tokenTtl, AccessRoute.SubmissionToken)),
                _ => Result<CacheInstruction>.Error("Unknown token type")
            };
        }

        var isFormPublic = await cache.GetOrCreateAsync(
            $"meta:form:{context.FormId}:is_public",
            async token => await IsFormPublicFromDbAsync(context.FormId, token),
            new HybridCacheEntryOptions { Expiration = _formMetadataTtl },
            tags: [$"form:{context.FormId}"],
            cancellationToken: cancellationToken
        );

        if (isFormPublic)
        {
            return Result<CacheInstruction>.Success(new($"auth:sub:form:{context.FormId}:public", _defaultTtl, AccessRoute.PublicForm));
        }

        var identityResult = await authorizationService.GetAuthorizationDataAsync(cancellationToken);
        if (!identityResult.IsSuccess || identityResult.Value!.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result<CacheInstruction>.Error("Form is private. Authentication required.");
        }

        return Result<CacheInstruction>.Success(new(
            $"auth:sub:form:{context.FormId}:user:{identityResult.Value.UserId}",
            _defaultTtl,
            AccessRoute.PrivateForm));
    }

    private async ValueTask<Cached<SubmissionAccessData>> ExecutePureLogicAsync(
        SubmissionAccessContext context,
        CacheInstruction instruction,
        CancellationToken ct)
    {
        // Dispatch to pure logic based on the enum
        var dataResult = instruction.Route switch
        {
            AccessRoute.AccessToken => ComputeAccessTokenLogic(context, out instruction),
            AccessRoute.SubmissionToken => await ComputeSubmissionTokenLogicAsync(context, ct),
            AccessRoute.PublicForm => ComputePublicFormLogic(context),
            AccessRoute.PrivateForm => await ComputePrivateFormLogicAsync(context, ct),
            _ => throw new InvalidOperationException("Unknown route")
        };

        if (!dataResult.IsSuccess)
        {
            throw new UnauthorizedAccessException(dataResult.Errors.FirstOrDefault());
        }

        return new Cached<SubmissionAccessData>
        {
            Data = dataResult.Value!,
            CachedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(instruction.Expiration),
            ETag = Guid.NewGuid().ToString("N")
        };
    }

    private Result<SubmissionAccessData> ComputeAccessTokenLogic(SubmissionAccessContext context, out CacheInstruction updatedInstruction)
    {
        updatedInstruction = default!;
        var accessResult = accessTokenService.ValidateAccessToken(context.Token!);
        if (!accessResult.IsSuccess)
        {
            return Result<SubmissionAccessData>.Error("Invalid access token");
        }


        var claims = accessResult.Value!;
        var dynamicTtl = TimeSpan.FromSeconds(Math.Max(1, (claims.ExpiresAt - DateTime.UtcNow - _safetyMargin).TotalSeconds));

        // Update the instruction so the envelope gets the correct dynamic TTL
        updatedInstruction = new CacheInstruction("", dynamicTtl, AccessRoute.AccessToken);

        return Result<SubmissionAccessData>.Success(BuildAccessTokenData(context.FormId, claims));
    }

    private async Task<Result<SubmissionAccessData>> ComputeSubmissionTokenLogicAsync(SubmissionAccessContext context, CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.ResolveTokenAsync(context.Token!, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return Result<SubmissionAccessData>.Error("Invalid or expired submission token");
        }

        return Result<SubmissionAccessData>.Success(new SubmissionAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = tokenResult.Value.ToString(),
            FormPermissions = [],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.ReviewSubmission]
        });
    }

    private Result<SubmissionAccessData> ComputePublicFormLogic(SubmissionAccessContext context)
    {
        return Result<SubmissionAccessData>.Success(new SubmissionAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = null,
            FormPermissions = [ResourcePermissions.Form.View],
            SubmissionPermissions = [ResourcePermissions.Submission.Create, ResourcePermissions.Submission.UploadFile]
        });
    }

    private async Task<Result<SubmissionAccessData>> ComputePrivateFormLogicAsync(SubmissionAccessContext context, CancellationToken cancellationToken)
    {
        var hasView = await authorizationService.HasPermissionAsync(Actions.Forms.View, cancellationToken);
        if (!hasView.IsSuccess || !hasView.Value)
        {
            return Result<SubmissionAccessData>.Error("Forbidden");
        }

        return Result<SubmissionAccessData>.Success(new SubmissionAccessData
        {
            FormId = context.FormId.ToString(),
            SubmissionId = null,
            FormPermissions = [],
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

    private static SubmissionAccessData BuildAccessTokenData(long formId, SubmissionAccessTokenClaims claims)
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

        return new SubmissionAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = claims.SubmissionId.ToString(),
            FormPermissions = [],
            SubmissionPermissions = submissionPermissions
        };
    }
}