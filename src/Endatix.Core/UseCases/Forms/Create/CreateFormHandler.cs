using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Forms.Create;

public class CreateFormHandler(
    IFormsRepository formsRepository,
    ITenantContext tenantContext,
    IMediator mediator) : ICommandHandler<CreateFormCommand, Result<Form>>
{
    public async Task<Result<Form>> Handle(CreateFormCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var newForm = new Form(
            tenantId: tenantContext.TenantId,
            name: request.Name,
            description: request.Description,
            isEnabled: request.IsEnabled,
            isPublic: true,
            webHookSettingsJson: request.WebHookSettingsJson);
        var newFormDefinition = new FormDefinition(tenantContext.TenantId, isDraft: true, jsonData: request.FormDefinitionJsonData);

        var form = await formsRepository.CreateFormWithDefinitionAsync(newForm, newFormDefinition, cancellationToken);

        await mediator.Publish(new FormCreatedEvent(form), cancellationToken);

        return Result<Form>.Created(form);
    }
}
