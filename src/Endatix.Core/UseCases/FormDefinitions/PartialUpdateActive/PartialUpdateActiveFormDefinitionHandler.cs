using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;

public class PartialUpdateActiveFormDefinitionHandler(IFormsRepository formRepository, IRepository<Submission> submissionsRepository) : ICommandHandler<PartialUpdateActiveFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(PartialUpdateActiveFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formWithActiveDefinition = await formRepository.SingleOrDefaultAsync(spec, cancellationToken);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;
        if (formWithActiveDefinition == null || activeDefinition == null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        if(await FormDefinitionHasSubmissions(activeDefinition.Id)) {
            var newFormDefinition = new FormDefinition(jsonData: request.JsonData);
            await formRepository.AddNewFormDefinitionAsync(formWithActiveDefinition, newFormDefinition, cancellationToken);
            newFormDefinition.UpdateDraftStatus(request.IsDraft);
            formWithActiveDefinition.SetActiveFormDefinition(newFormDefinition);
        }
        else {
            activeDefinition.UpdateSchema(request.JsonData);
            activeDefinition.UpdateDraftStatus(request.IsDraft);
        }

        await formRepository.UpdateAsync(formWithActiveDefinition, cancellationToken);
        return Result.Success(activeDefinition);
    }

    private async Task<bool> FormDefinitionHasSubmissions(long definitionId) {
        var spec = new SubmissionsTotalCountByFormDefinitionIdSpec(definitionId);
        var count = await submissionsRepository.CountAsync(spec);
        return count > 0;
    }
}
