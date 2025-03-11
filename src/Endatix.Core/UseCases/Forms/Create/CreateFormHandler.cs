using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.Create;

public class CreateFormHandler(IFormsRepository formsRepository, ITenantContext tenantContext) : ICommandHandler<CreateFormCommand, Result<Form>>
{
    public async Task<Result<Form>> Handle(CreateFormCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var newForm = new Form(tenantContext.TenantId, request.Name, request.Description, request.IsEnabled);
        var newFormDefinition = new FormDefinition(tenantContext.TenantId, isDraft: true, jsonData: request.FormDefinitionJsonData);

        var form = await formsRepository.CreateFormWithDefinitionAsync(newForm, newFormDefinition, cancellationToken);

        return Result<Form>.Created(form);
    }
}
