using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions.GetActive;

public class GetActiveFormDefinitionHandler(IFormsRepository formRepository) : IQueryHandler<GetActiveFormDefinitionQuery, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(GetActiveFormDefinitionQuery request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formWithActiveDefinition = await formRepository.SingleOrDefaultAsync(spec, cancellationToken);

        if (formWithActiveDefinition == null || formWithActiveDefinition.ActiveDefinition == null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        return Result.Success(formWithActiveDefinition.ActiveDefinition!);
    }
}
