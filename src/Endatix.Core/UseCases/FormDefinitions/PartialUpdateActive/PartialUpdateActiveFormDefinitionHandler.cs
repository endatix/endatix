using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;

public class PartialUpdateActiveFormDefinitionHandler(IFormsRepository formsRepository,
    IRepository<Submission> submissionsRepository,
    IMediator mediator, ITenantContext tenant) : ICommandHandler<PartialUpdateActiveFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(PartialUpdateActiveFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        spec.InTenant(tenant);
        var formWithActiveDefinition = await formsRepository.SingleOrDefaultAsync(spec, cancellationToken);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;
        if (formWithActiveDefinition is null || activeDefinition is null || activeDefinition.TenantId != formWithActiveDefinition.TenantId)
        {
            return Result.NotFound("Active form definition not found.");
        }

        activeDefinition = await ApplyAsync(formWithActiveDefinition, activeDefinition, request);
        await formsRepository.UpdateAsync(formWithActiveDefinition, cancellationToken);
        await mediator.Publish(new FormDefinitionChangedEvent(request.FormId), cancellationToken);

        return Result.Success(activeDefinition);
    }

    private async Task<FormDefinition> ApplyAsync(Form form, FormDefinition activeDefinition, PartialUpdateActiveFormDefinitionCommand request)
    {
        if (await ShouldCreateNewFormDefinitionAsync(activeDefinition, request))
        {
            return AddDefinition(form, request);
        }
        form.UpdateActiveDefinitionSchema(request.JsonData, request.IsDraft);
        return activeDefinition;
    }

    private static FormDefinition AddDefinition(Form form, PartialUpdateActiveFormDefinitionCommand request)
    {
        var definition = new FormDefinition(form.TenantId, request.IsDraft ?? false, request.JsonData);
        form.AddFormDefinition(definition);
        if (!definition.IsDraft)
        {
            form.SetActiveFormDefinition(definition);
        }
        return definition;
    }

    private async Task<bool> ShouldCreateNewFormDefinitionAsync(
        FormDefinition formDefinition,
        PartialUpdateActiveFormDefinitionCommand request)
    {
        return formDefinition.JsonData != request.JsonData && await submissionsRepository.AnyAsync(new SubmissionsTotalCountByFormDefinitionIdSpec(formDefinition.Id));
    }
}
