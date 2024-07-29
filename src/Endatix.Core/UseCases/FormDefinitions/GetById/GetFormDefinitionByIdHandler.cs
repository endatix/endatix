using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.GetById;

public class GetFormDefinitionByIdHandler(IRepository<FormDefinition> _repository) : IQueryHandler<GetFormDefinitionByIdQuery, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(GetFormDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        var formDefinition = await _repository.GetByIdAsync(request.DefinitionId, cancellationToken);
        if (formDefinition == null || formDefinition.FormId != request.FormId)
        {
            return Result.NotFound("Form definition not found.");
        }
        return Result.Success(formDefinition);
    }
}
