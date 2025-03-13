using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.FormTemplates.List;

public class ListFormTemplatesHandler(IRepository<FormTemplate> repository) 
    : IQueryHandler<ListFormTemplatesQuery, Result<IEnumerable<FormTemplateDto>>>
{
    public async Task<Result<IEnumerable<FormTemplateDto>>> Handle(ListFormTemplatesQuery request, CancellationToken cancellationToken)
    {
        var pagingParams = new PagingParameters(request.Page, request.PageSize);
        var spec = new FormTemplatesSpec(pagingParams);
        IEnumerable<FormTemplateDto> formTemplates = await repository.ListAsync(spec, cancellationToken);
        return Result.Success(formTemplates);
    }
}
