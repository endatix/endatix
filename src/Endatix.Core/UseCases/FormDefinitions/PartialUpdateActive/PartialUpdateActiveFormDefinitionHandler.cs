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

/// <summary>Applies a partial update to the active definition.</summary>
[DirectCallOnly]
public class PartialUpdateActiveFormDefinitionHandler : ICommandHandler<PartialUpdateActiveFormDefinitionCommand, Result<FormDefinition>>
{
    private readonly ActiveDefinitions _definitions;
    private readonly IMediator _mediator;
    private readonly IRepository<Submission> _submissionsRepository;

    public PartialUpdateActiveFormDefinitionHandler(IFormsRepository formsRepository, IRepository<Submission> submissionsRepository, IMediator mediator)
        : this(new ActiveDefinitions(formsRepository, ContentQueryScope.Repository), submissionsRepository, mediator) { }

    private PartialUpdateActiveFormDefinitionHandler(ActiveDefinitions definitions, IRepository<Submission> submissionsRepository, IMediator mediator)
    {
        _definitions = definitions;
        _mediator = mediator;
        _submissionsRepository = submissionsRepository;
    }

    /// <inheritdoc />
    public async Task<Result<FormDefinition>> Handle(PartialUpdateActiveFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var formWithActiveDefinition = await _definitions.FindFormAsync(request.FormId, cancellationToken);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;
        if (formWithActiveDefinition is null || activeDefinition is null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        activeDefinition = await ApplyAsync(formWithActiveDefinition, activeDefinition, request);
        await _definitions.UpdateAsync(formWithActiveDefinition, cancellationToken);
        await _mediator.Publish(new FormDefinitionChangedEvent(request.FormId), cancellationToken);

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
        return formDefinition.JsonData != request.JsonData && await _submissionsRepository.AnyAsync(new SubmissionsTotalCountByFormDefinitionIdSpec(formDefinition.Id));
    }

    internal sealed class Registered(ActiveDefinitions definitions, IRepository<Submission> submissionsRepository, IMediator mediator)
        : RegisteredContentHandler<PartialUpdateActiveFormDefinitionCommand, Result<FormDefinition>>(
            new PartialUpdateActiveFormDefinitionHandler(definitions, submissionsRepository, mediator));
}
