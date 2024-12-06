using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.GetById;

public class GetFormDefinitionByIdHandler(IFormsRepository formsRepository) : IQueryHandler<GetFormDefinitionByIdQuery, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(GetFormDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        var specification = new DefinitionByFormAndDefinitionIdSpec(request.FormId, request.DefinitionId);
        var formDefinition = await formsRepository.SingleOrDefaultAsync(specification, cancellationToken);
        if (formDefinition == null || formDefinition.FormId != request.FormId)
        {
            return Result.NotFound("Form definition not found.");
        }

        return Result.Success(formDefinition);
    }
}
