using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;
using MediatR;

namespace Endatix.Core.UseCases.Forms.Create;

/// <summary>
/// Handler for creating a form.
/// </summary>
public class CreateFormHandler(
    IFormsRepository formsRepository,
    ITenantContext tenantContext,
    IMediator mediator,
    FolderAssignmentPolicy folderAssignmentPolicy) : ICommandHandler<CreateFormCommand, Result<Form>>
{
    /// <inheritdoc/>
    public async Task<Result<Form>> Handle(CreateFormCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var folderCheck = await folderAssignmentPolicy.EnsureFolderAssignmentValidAsync(request.FolderId, cancellationToken);
        if (!folderCheck.IsOk())
        {
            return folderCheck.ToErrorResult<Form>();
        }

        var newForm = new Form(
            tenantId: tenantContext.TenantId,
            name: request.Name,
            description: request.Description,
            isEnabled: request.IsEnabled,
            isPublic: false,
            limitOnePerUser: request.LimitOnePerUser,
            metadata: request.Metadata,
            webHookSettingsJson: request.WebHookSettingsJson,
            folderId: request.FolderId);
        var newFormDefinition = new FormDefinition(tenantContext.TenantId, isDraft: true, jsonData: request.FormDefinitionJsonData);

        var form = await formsRepository.CreateFormWithDefinitionAsync(newForm, newFormDefinition, cancellationToken);

        await mediator.Publish(new FormCreatedEvent(form), cancellationToken);

        return Result<Form>.Created(form);
    }
}
