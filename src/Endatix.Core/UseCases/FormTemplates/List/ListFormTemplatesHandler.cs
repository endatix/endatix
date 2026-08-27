using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.FormTemplates.List;

public class ListFormTemplatesHandler(IRepository<FormTemplate> repository)
    : IQueryHandler<ListFormTemplatesQuery, Result<Paged<FormTemplateDto>>>
{
    public async Task<Result<Paged<FormTemplateDto>>> Handle(
        ListFormTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var pagingParams = new PagingParameters(request.Page, request.PageSize);
        var filterParams = CreateFilterParameters(request.FilterExpressions, request.FolderId);

        var countSpec = new FormTemplatesListFilterSpec(
            filterParams,
            request.Created,
            request.Modified);
        var totalRecords = await repository.CountAsync(countSpec, cancellationToken);

        var page = Paged<FormTemplateDto>.ResolvePage(
            pagingParams.Page,
            pagingParams.PageSize,
            totalRecords);

        IReadOnlyList<FormTemplateDto> items = [];
        if (totalRecords > 0)
        {
            var spec = new FormTemplatesSpec(
                new PagingParameters(page, pagingParams.PageSize),
                filterParams,
                request.SortBy,
                request.SortDescending,
                request.Created,
                request.Modified);
            items = [.. await repository.ListAsync(spec, cancellationToken)];
        }

        return Result.Success(Paged<FormTemplateDto>.FromPage(
            page,
            pagingParams.PageSize,
            totalRecords,
            items));
    }

    private static FilterParameters CreateFilterParameters(
        IEnumerable<string>? filterExpressions,
        long? folderId)
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
