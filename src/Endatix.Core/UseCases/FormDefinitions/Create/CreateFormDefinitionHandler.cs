using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.FormDefinitions.Create;

public class CreateFormDefinitionHandler(IFormsRepository formsRepository,
    ITenantContext tenantContext,
    IMediator mediator) : ICommandHandler<CreateFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(CreateFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var form = await formsRepository.FindAsync(request.FormId, tenantContext, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }

        var newFormDefinition = AddDefinition(form, request);
        await formsRepository.UpdateAsync(form, cancellationToken);
        await mediator.Publish(new FormDefinitionChangedEvent(form.Id), cancellationToken);

        return Result<FormDefinition>.Created(newFormDefinition);
    }

    private static FormDefinition AddDefinition(Form form, CreateFormDefinitionCommand request)
    {
        var newFormDefinition = new FormDefinition(form.TenantId, request.IsDraft, request.JsonData);
        form.AddFormDefinition(newFormDefinition);
        if (!newFormDefinition.IsDraft)
        {
            form.SetActiveFormDefinition(newFormDefinition);
        }

        return newFormDefinition;
    }
}
