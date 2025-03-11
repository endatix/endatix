using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.FormDefinitions.List;

public class ListFormDefinitionsHandler(IRepository<FormDefinition> _repository) : IQueryHandler<ListFormDefinitionsQuery, Result<IEnumerable<FormDefinition>>>
{
    public async Task<Result<IEnumerable<FormDefinition>>> Handle(ListFormDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var pagingParams = new PagingParameters(request.Page, request.PageSize);
        var spec = new FormDefinitionsByFormIdSpec(request.FormId, pagingParams);
        IEnumerable<FormDefinition> formDefinitions = await _repository.ListAsync(spec, cancellationToken);
        if (formDefinitions.Any())
        {
            return Result.Success(formDefinitions);
        }

        return Result.NotFound("Form not found.");
    }
}
