using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions.GetActive;

public class GetActiveFormDefinitionHandler(IRepository<FormDefinition> _repository) : IQueryHandler<GetActiveFormDefinitionQuery, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(GetActiveFormDefinitionQuery request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formDefinition = await _repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (formDefinition == null)
        {
            return Result.NotFound("Active form definition not found.");
        }
        return Result.Success(formDefinition);
    }
}
