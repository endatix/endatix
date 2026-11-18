using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Authorization.PublicForm;
using Endatix.Infrastructure.Features.AccessControl;

namespace Endatix.Infrastructure.Features.Authorization.PublicForm;

/// <summary>
/// Handles <see cref="CreateFormAccessTokenCommand"/>.
/// </summary>
public sealed class CreateFormAccessTokenHandler(
    IFormRuntimeScopeReader runtimeScopeReader,
    IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> publicFormAccessPolicy,
    IFormAccessTokenService formFrameTokenService,
    ICurrentUserAuthorizationService authorizationService
) : ICommandHandler<CreateFormAccessTokenCommand, Result<FormAccessTokenDto>>
{
    /// <inheritdoc />
    public async Task<Result<FormAccessTokenDto>> Handle(CreateFormAccessTokenCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request);

        if (request.AccessTokenType is SubmissionTokenType.FormToken)
        {
            return Result.Forbidden("Cannot use FormToken to mint new token");
        }

        PublicFormAccessContext accessContext = new(request.FormId, request.AccessToken, request.AccessTokenType);
        var accessResult = await publicFormAccessPolicy
            .GetAccessData(accessContext, cancellationToken);

        if (!accessResult.IsSuccess)
        {
            return accessResult.ToErrorResult<FormAccessTokenDto>();
        }

        var scope = await runtimeScopeReader.GetAsync(request.FormId, cancellationToken).ConfigureAwait(false);
        if (scope is null || !scope.IsEnabled)
        {
            return Result.NotFound("Form not found");
        }

        var isPublicGrant = scope.IsPublic || !await HasPrivateFormAccessAsync(request, cancellationToken);
        return formFrameTokenService.CreateToken(request.FormId, scope.TenantId, isPublicGrant);
    }

    /// <summary>
    /// Whether the caller may open the form while it is private: a share-link access token the policy accepted for
    /// this form, or a user with private-form access. Read from the caller's credentials, not from the form cache,
    /// so a caller let in while a cache still says public never gets a token that outlives the form going private.
    /// </summary>
    private async Task<bool> HasPrivateFormAccessAsync(CreateFormAccessTokenCommand request, CancellationToken cancellationToken)
    {
        if (request.AccessTokenType is SubmissionTokenType.AccessToken && !string.IsNullOrWhiteSpace(request.AccessToken))
        {
            return true;
        }

        var permission = await authorizationService.HasPermissionAsync(Actions.Access.PrivateForms, cancellationToken);
        return permission.IsSuccess && permission.Value;
    }
}
