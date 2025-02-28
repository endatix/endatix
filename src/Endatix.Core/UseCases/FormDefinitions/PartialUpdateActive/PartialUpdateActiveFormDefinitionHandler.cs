using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;

public class PartialUpdateActiveFormDefinitionHandler(IFormsRepository formsRepository, IRepository<Submission> submissionsRepository, IEntityFactory entityFactory) : ICommandHandler<PartialUpdateActiveFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(PartialUpdateActiveFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formWithActiveDefinition = await formsRepository.SingleOrDefaultAsync(spec, cancellationToken);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;
        if (formWithActiveDefinition == null || activeDefinition == null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        if(await ShouldCreateNewFormDefinitionAsync(activeDefinition, request)) {
            var newFormDefinition = entityFactory.CreateFormDefinition(request.IsDraft??false, request.JsonData);
            formWithActiveDefinition.AddFormDefinition(newFormDefinition);
            
            if (!newFormDefinition.IsDraft) {
                formWithActiveDefinition.SetActiveFormDefinition(newFormDefinition);
            }
        }
        else {
            activeDefinition.UpdateSchema(request.JsonData);
            activeDefinition.UpdateDraftStatus(request.IsDraft);
        }

        await formsRepository.UpdateAsync(formWithActiveDefinition, cancellationToken);
        return Result.Success(activeDefinition);
    }

    private async Task<bool> FormDefinitionHasSubmissions(long definitionId) {
        var spec = new SubmissionsTotalCountByFormDefinitionIdSpec(definitionId);
        return await submissionsRepository.AnyAsync(spec);
    }

    private async Task<bool> ShouldCreateNewFormDefinitionAsync(FormDefinition formDefinition, PartialUpdateActiveFormDefinitionCommand request) {
        return formDefinition.JsonData != request.JsonData && await FormDefinitionHasSubmissions(formDefinition.Id);
    }
}
