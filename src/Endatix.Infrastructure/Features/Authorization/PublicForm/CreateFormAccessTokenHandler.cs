using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Authorization.PublicForm;
using Endatix.Infrastructure.Features.AccessControl;

namespace Endatix.Infrastructure.Features.Authorization.PublicForm;

/// <summary>
/// Handles <see cref="CreateFormAccessTokenCommand"/>.
/// </summary>
public sealed class CreateFormAccessTokenHandler(
    IRepository<Form> formRepository,
    IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> publicFormAccessPolicy,
    IFormAccessTokenService formFrameTokenService
) : ICommandHandler<CreateFormAccessTokenCommand, Result<FormAccessTokenDto>>
{
    /// <inheritdoc />
    public async Task<Result<FormAccessTokenDto>> Handle(CreateFormAccessTokenCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request);

        PublicFormAccessContext accessContext = new(request.FormId, request.AccessToken, request.AccessTokenType);
        var accessResult = await publicFormAccessPolicy
            .GetAccessData(accessContext, cancellationToken);

        if (!accessResult.IsSuccess)
        {
            return accessResult.ToErrorResult<FormAccessTokenDto>();
        }

        var form = await formRepository.FirstOrDefaultAsync(
            new FormSpecifications.ByIdWithRelatedForPublicAccess(request.FormId),
            cancellationToken).ConfigureAwait(false);

        if (form is null)
        {
            return Result.NotFound("Form not found");
        }

        return formFrameTokenService.CreateToken(request.FormId, form.TenantId);
    }
}
