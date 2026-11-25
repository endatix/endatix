using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.FormDefinitions.UpdateActive;

/// <summary>Replaces the active definition schema.</summary>
[DirectCallOnly]
public class UpdateActiveFormDefinitionHandler : ICommandHandler<UpdateActiveFormDefinitionCommand, Result<FormDefinition>>
{
    private readonly ActiveDefinitions _definitions;
    private readonly IMediator _mediator;

    public UpdateActiveFormDefinitionHandler(IFormsRepository formRepository, IMediator mediator)
        : this(new ActiveDefinitions(formRepository, ContentQueryScope.Repository), mediator) { }

    private UpdateActiveFormDefinitionHandler(ActiveDefinitions definitions, IMediator mediator)
    {
        _definitions = definitions;
        _mediator = mediator;
    }

    /// <inheritdoc />
    public async Task<Result<FormDefinition>> Handle(UpdateActiveFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var formWithActiveDefinition = await _definitions.FindFormAsync(request.FormId, cancellationToken);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;
        if (formWithActiveDefinition is null || activeDefinition is null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        formWithActiveDefinition.UpdateActiveDefinitionSchema(request.JsonData, request.IsDraft);

        await _definitions.UpdateAsync(formWithActiveDefinition, cancellationToken);
        await _mediator.Publish(new FormDefinitionChangedEvent(request.FormId), cancellationToken);

        return Result.Success(activeDefinition);
    }

    internal sealed class Registered(ActiveDefinitions definitions, IMediator mediator)
        : RegisteredContentHandler<UpdateActiveFormDefinitionCommand, Result<FormDefinition>>(
            new UpdateActiveFormDefinitionHandler(definitions, mediator));
}
