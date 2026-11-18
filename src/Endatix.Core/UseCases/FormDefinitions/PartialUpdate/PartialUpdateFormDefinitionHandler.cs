using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.FormDefinitions.PartialUpdate;

/// <summary>
/// Updates one definition of a form by id, then publishes <see cref="FormDefinitionChangedEvent"/>.
/// </summary>
/// <param name="repository">The form definitions.</param>
/// <param name="mediator">Publishes the in-process change notification.</param>
public class PartialUpdateFormDefinitionHandler(
    IRepository<FormDefinition> repository,
    IMediator mediator) : ICommandHandler<PartialUpdateFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(PartialUpdateFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var formDefinition = await repository.GetByIdAsync(request.DefinitionId, cancellationToken);
        if (formDefinition == null || formDefinition.FormId != request.FormId)
        {
            return Result.NotFound("Form definition not found.");
        }

        formDefinition.UpdateSchema(request.JsonData);
        formDefinition.UpdateDraftStatus(request.IsDraft);

        await repository.UpdateAsync(formDefinition, cancellationToken);
        await mediator.Publish(new FormDefinitionChangedEvent(request.FormId), cancellationToken);
        return Result.Success(formDefinition);
    }
}
