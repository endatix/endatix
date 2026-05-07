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
        var filterParams = CreateFilterParameters(request.FilterExpressions, request.FolderId);
        var spec = new FormTemplatesSpec(pagingParams, filterParams);
        IEnumerable<FormTemplateDto> formTemplates = await repository.ListAsync(spec, cancellationToken);
        return Result.Success(formTemplates);
    }

    private static FilterParameters CreateFilterParameters(IEnumerable<string>? filterExpressions, long? folderId)
    {
        var filterList = new List<string>();
        if (filterExpressions is not null)
        {
            filterList.AddRange(filterExpressions);
        }

        if (folderId.HasValue)
        {
            filterList.Add($"FolderId:{folderId.Value}");
        }

        return new FilterParameters(filterList);
    }
}
