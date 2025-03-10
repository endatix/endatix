using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.Create;

public class CreateFormDefinitionHandler(IFormsRepository formsRepository, ITenantContext tenantContext) : ICommandHandler<CreateFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(CreateFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var form = await formsRepository.GetByIdAsync(request.FormId, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }

        var newFormDefinition = new FormDefinition(tenantContext.TenantId, request.IsDraft, request.JsonData);
        form.AddFormDefinition(newFormDefinition);
        if (!newFormDefinition.IsDraft)
        {
            form.SetActiveFormDefinition(newFormDefinition);
        }
        await formsRepository.UpdateAsync(form, cancellationToken);

        return Result<FormDefinition>.Created(newFormDefinition);
    }
}
