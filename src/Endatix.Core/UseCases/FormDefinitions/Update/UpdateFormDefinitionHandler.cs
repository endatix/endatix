using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.FormDefinitions.Update;

/// <summary>
/// Replaces one definition of a form by id, then publishes <see cref="FormDefinitionChangedEvent"/>.
/// </summary>
/// <param name="repository">The form definitions.</param>
/// <param name="mediator">Publishes the in-process change notification.</param>
[DirectCallOnly]
public class UpdateFormDefinitionHandler(IRepository<FormDefinition> repository,
    IMediator mediator) : ICommandHandler<UpdateFormDefinitionCommand, Result<FormDefinition>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    public async Task<Result<FormDefinition>> Handle(UpdateFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var formDefinition = await Selection.FindAsync(repository, request.DefinitionId, cancellationToken);
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

    internal sealed class Registered(IRepository<FormDefinition> repository, IMediator mediator, ContentQueryScope tenant)
        : RegisteredContentHandler<UpdateFormDefinitionCommand, Result<FormDefinition>>(
            new UpdateFormDefinitionHandler(repository, mediator) { Selection = tenant });
}
