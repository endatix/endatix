using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.FormDefinitions.UpdateActive;

public class UpdateActiveFormDefinitionHandler(IFormsRepository formRepository,
    IMediator mediator,
    ManagedFormContentAccess? access) : ICommandHandler<UpdateActiveFormDefinitionCommand, Result<FormDefinition>>
{
    /// <summary>Creates a handler with dormant account standing.</summary>
    public UpdateActiveFormDefinitionHandler(IFormsRepository formRepository, IMediator mediator)
        : this(formRepository, mediator, null) { }

    public async Task<Result<FormDefinition>> Handle(UpdateActiveFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        if (access is not null && await access.ActiveAsync(formRepository, request.FormId, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await ExecuteAsync(request, cancellationToken);
    }

    private async Task<Result<FormDefinition>> ExecuteAsync(UpdateActiveFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formWithActiveDefinition = await formRepository.SingleOrDefaultAsync(spec, cancellationToken);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;
        if (formWithActiveDefinition == null || activeDefinition == null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        formWithActiveDefinition.UpdateActiveDefinitionSchema(request.JsonData, request.IsDraft);

        await formRepository.UpdateAsync(formWithActiveDefinition, cancellationToken);
        await mediator.Publish(new FormDefinitionChangedEvent(request.FormId), cancellationToken);

        return Result.Success(activeDefinition);
    }
}
