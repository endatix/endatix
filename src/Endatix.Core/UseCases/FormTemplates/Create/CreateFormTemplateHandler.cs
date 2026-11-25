using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;

namespace Endatix.Core.UseCases.FormTemplates.Create;

/// <summary>Creates a form template in the current tenant.</summary>
public class CreateFormTemplateHandler(IRepository<FormTemplate> repository,
    ITenantContext tenantContext,
    FolderAssignmentPolicy folderAssignmentPolicy, ManagedContentScope? scope)
    : ICommandHandler<CreateFormTemplateCommand, Result<FormTemplate>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public CreateFormTemplateHandler(IRepository<FormTemplate> repository,
        ITenantContext tenantContext,
        FolderAssignmentPolicy folderAssignmentPolicy)
        : this(repository, tenantContext, folderAssignmentPolicy, null) { }

    public async Task<Result<FormTemplate>> Handle(CreateFormTemplateCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var folderCheck = await folderAssignmentPolicy.EnsureFolderAssignmentValidAsync(request.FolderId, cancellationToken);
        if (!folderCheck.IsOk())
        {
            return folderCheck.ToErrorResult<FormTemplate>();
        }

        if (scope is not null && await scope.RefusalAsync<FormTemplate>(cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var formTemplate = new FormTemplate(tenantContext.TenantId, request.Name, request.Description, request.JsonData, request.FolderId);

        await repository.AddAsync(formTemplate, cancellationToken);
        return Result<FormTemplate>.Created(formTemplate);
    }
}
