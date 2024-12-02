using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.Update;

public class UpdateFormDefinitionHandler(IRepository<FormDefinition> repository) : ICommandHandler<UpdateFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(UpdateFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var formDefinition = await repository.GetByIdAsync(request.DefinitionId, cancellationToken);
        if (formDefinition == null || formDefinition.FormId != request.FormId)
        {
            return Result.NotFound("Form definition not found.");
        }

        formDefinition.UpdateSchema(request.JsonData);
        formDefinition.UpdateDraftStatus(request.IsDraft);
        await repository.UpdateAsync(formDefinition, cancellationToken);
        return Result.Success(formDefinition);
    }
}
