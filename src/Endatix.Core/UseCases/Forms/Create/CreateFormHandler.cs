using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
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
    FolderAssignmentPolicy folderAssignmentPolicy,
    AccountStanding standing) : ICommandHandler<CreateFormCommand, Result<Form>>
{
    public CreateFormHandler(IFormsRepository formsRepository, ITenantContext tenantContext, FolderAssignmentPolicy folderAssignmentPolicy)
        : this(formsRepository, tenantContext, folderAssignmentPolicy, new AccountStanding()) { }

    /// <inheritdoc/>
    public async Task<Result<Form>> Handle(CreateFormCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var check = await CheckAsync(request, cancellationToken);
        if (!check.IsOk())
        {
            return check.ToErrorResult<Form>();
        }

        if (standing.IsEnforced)
        {
            return await standing.RefusalAsync<Form>(cancellationToken) ?? await CreateAfterStandingAsync(request, cancellationToken);
        }
        return await CreateAsync(request, cancellationToken);
    }

    private async Task<Result<Form>> CreateAfterStandingAsync(CreateFormCommand request, CancellationToken ct)
    {
        var folder = await folderAssignmentPolicy.EnsureFolderAssignmentValidAsync(request.FolderId, ct);
        return folder.IsOk() ? await CreateAsync(request, ct) : folder.ToErrorResult<Form>();
    }

    private async Task<Result<Form>> CreateAsync(CreateFormCommand request, CancellationToken cancellationToken)
    {
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
        var definition = new FormDefinition(tenantContext.TenantId, isDraft: true, jsonData: request.FormDefinitionJsonData);
        var form = await formsRepository.CreateFormWithDefinitionAsync(Form.Create(createArgs), definition, cancellationToken);
        return Result<Form>.Created(form);
    }

    // Webhook settings that still use a retired event key are refused before the folder is checked.
    private async Task<Result> CheckAsync(CreateFormCommand request, CancellationToken cancellationToken) =>
        WebHookConfiguration.RetiredKeyError(request.WebHookSettingsJson) is { } retiredKey
            ? Result.Invalid(new ValidationError(retiredKey))
            : await folderAssignmentPolicy.EnsureFolderAssignmentValidAsync(request.FolderId, cancellationToken);
}
