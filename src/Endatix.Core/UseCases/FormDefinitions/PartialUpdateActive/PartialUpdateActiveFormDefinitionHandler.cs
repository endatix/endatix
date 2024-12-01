using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;

public class PartialUpdateActiveFormDefinitionHandler(IRepository<FormDefinition> _repository) : ICommandHandler<PartialUpdateActiveFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(PartialUpdateActiveFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formDefinition = await _repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (formDefinition == null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        formDefinition.Update(request.JsonData, request.IsDraft, request.IsActive);

        await _repository.UpdateAsync(formDefinition, cancellationToken);
        return Result.Success(formDefinition);
    }
}
