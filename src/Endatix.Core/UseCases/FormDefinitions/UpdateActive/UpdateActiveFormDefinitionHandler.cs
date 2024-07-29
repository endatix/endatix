using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions.UpdateActive;

public class UpdateActiveFormDefinitionHandler(IRepository<FormDefinition> _repository) : ICommandHandler<UpdateActiveFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(UpdateActiveFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formDefinition = await _repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (formDefinition == null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        formDefinition.IsDraft = request.IsDraft;
        formDefinition.JsonData = request.JsonData;
        formDefinition.IsActive = request.IsActive;
        await _repository.UpdateAsync(formDefinition, cancellationToken);
        return Result.Success(formDefinition);
    }
}
