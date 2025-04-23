using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions.GetActive;

public class GetActiveFormDefinitionHandler(IFormsRepository formRepository) : IQueryHandler<GetActiveFormDefinitionQuery, Result<ActiveDefinitionDto>>
{
    public async Task<Result<ActiveDefinitionDto>> Handle(GetActiveFormDefinitionQuery request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formWithActiveDefinition = await formRepository.SingleOrDefaultAsync(spec, cancellationToken);

        if (formWithActiveDefinition == null || formWithActiveDefinition.ActiveDefinition == null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        var activeDefinitionDto = new ActiveDefinitionDto(
            formWithActiveDefinition.ActiveDefinition,
            formWithActiveDefinition.Theme?.JsonData);

        return Result.Success(activeDefinitionDto);
    }
}
