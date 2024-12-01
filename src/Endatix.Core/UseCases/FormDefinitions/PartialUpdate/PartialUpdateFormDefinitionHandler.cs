using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.PartialUpdate;

public class PartialUpdateFormDefinitionHandler(IRepository<FormDefinition> _repository) : ICommandHandler<PartialUpdateFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(PartialUpdateFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var formDefinition = await _repository.GetByIdAsync(request.DefinitionId, cancellationToken);
        if (formDefinition == null || formDefinition.FormId != request.FormId)
        {
            return Result.NotFound("Form definition not found.");
        }

        formDefinition.Update(request.JsonData, request.IsDraft, request.IsActive);
        await _repository.UpdateAsync(formDefinition, cancellationToken);
        return Result.Success(formDefinition);
    }
}
