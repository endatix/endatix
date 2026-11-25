using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.FormDefinitions.UpdateActive;

public class UpdateActiveFormDefinitionHandler(IFormsRepository formRepository,
    IMediator mediator, ITenantContext tenant) : ICommandHandler<UpdateActiveFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(UpdateActiveFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        spec.InTenant(tenant);
        var formWithActiveDefinition = await formRepository.SingleOrDefaultAsync(spec, cancellationToken);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;
        if (formWithActiveDefinition is null || activeDefinition is null || activeDefinition.TenantId != formWithActiveDefinition.TenantId)
        {
            return Result.NotFound("Active form definition not found.");
        }

        formWithActiveDefinition.UpdateActiveDefinitionSchema(request.JsonData, request.IsDraft);

        await formRepository.UpdateAsync(formWithActiveDefinition, cancellationToken);
        await mediator.Publish(new FormDefinitionChangedEvent(request.FormId), cancellationToken);

        return Result.Success(activeDefinition);
    }
}
