using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;

namespace Endatix.Core.UseCases.Forms.Create;

/// <summary>
/// Handler for creating a form.
/// </summary>
public class CreateFormHandler(
    IFormsRepository formsRepository,
    ITenantContext tenantContext,
    FolderAssignmentPolicy folderAssignmentPolicy) : ICommandHandler<CreateFormCommand, Result<Form>>
{
    /// <inheritdoc/>
    public async Task<Result<Form>> Handle(CreateFormCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var check = await CheckAsync(request, cancellationToken);
        if (!check.IsOk())
        {
            return check.ToErrorResult<Form>();
        }

        FormCreateArgs createArgs = new(
            TenantId: tenantContext.TenantId,
            Name: request.Name,
            Description: request.Description,
            IsEnabled: request.IsEnabled,
            IsPublic: false,
            LimitOnePerUser: request.LimitOnePerUser,
            Metadata: request.Metadata,
            WebHookSettingsJson: request.WebHookSettingsJson,
            FolderId: request.FolderId,
            SubmissionTokenExpiryHours: request.SubmissionTokenExpiryHours);
        var newForm = Form.Create(createArgs);
        var newFormDefinition = new FormDefinition(tenantContext.TenantId, isDraft: true, jsonData: request.FormDefinitionJsonData);

        // form.created is captured to the outbox (→ webhook) inside CreateFormWithDefinitionAsync via
        // form.RaiseCreated(); there are no in-process MediatR subscribers for it, so nothing is published here.
        var form = await formsRepository.CreateFormWithDefinitionAsync(newForm, newFormDefinition, cancellationToken);

        return Result<Form>.Created(form);
    }

    // Webhook settings that still use a retired event key are refused before the folder is checked.
    private async Task<Result> CheckAsync(CreateFormCommand request, CancellationToken cancellationToken) =>
        WebHookConfiguration.RetiredKeyError(request.WebHookSettingsJson) is { } retiredKey
            ? Result.Invalid(new ValidationError(retiredKey))
            : await folderAssignmentPolicy.EnsureFolderAssignmentValidAsync(request.FolderId, cancellationToken);
}
