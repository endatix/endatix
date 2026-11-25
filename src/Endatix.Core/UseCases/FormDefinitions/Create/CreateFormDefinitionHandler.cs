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
    IMediator mediator,
    ManagedFormContentAccess? access) : ICommandHandler<CreateFormDefinitionCommand, Result<FormDefinition>>
{
    /// <summary>Creates a handler with dormant account standing.</summary>
    public CreateFormDefinitionHandler(IFormsRepository formsRepository, ITenantContext tenantContext, IMediator mediator)
        : this(formsRepository, tenantContext, mediator, null) { }

    public async Task<Result<FormDefinition>> Handle(CreateFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        if (access is not null && await access.FormAsync<FormDefinition>(request.FormId, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await ExecuteAsync(request, cancellationToken);
    }

    private async Task<Result<FormDefinition>> ExecuteAsync(CreateFormDefinitionCommand request, CancellationToken cancellationToken)
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
        await mediator.Publish(new FormDefinitionChangedEvent(form.Id), cancellationToken);

        return Result<FormDefinition>.Created(newFormDefinition);
    }
}
