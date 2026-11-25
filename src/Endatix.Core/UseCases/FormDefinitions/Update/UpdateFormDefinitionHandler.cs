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
/// <param name="access">The managed operation’s visibility and standing decision.</param>
public class UpdateFormDefinitionHandler(IRepository<FormDefinition> repository,
    IMediator mediator,
    ManagedFormContentAccess? access) : ICommandHandler<UpdateFormDefinitionCommand, Result<FormDefinition>>
{
    /// <summary>Creates a handler with dormant account standing.</summary>
    public UpdateFormDefinitionHandler(IRepository<FormDefinition> repository, IMediator mediator)
        : this(repository, mediator, null) { }

    public async Task<Result<FormDefinition>> Handle(UpdateFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        if (access is not null && await access.DefinitionAsync(request.FormId, request.DefinitionId, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await ExecuteAsync(request, cancellationToken);
    }

    private async Task<Result<FormDefinition>> ExecuteAsync(UpdateFormDefinitionCommand request, CancellationToken cancellationToken)
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
