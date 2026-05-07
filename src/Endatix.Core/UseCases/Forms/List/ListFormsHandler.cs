using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.Forms.List;

public class ListFormsHandler(IFormsRepository repository) : IQueryHandler<ListFormsQuery, Result<IEnumerable<FormDto>>>
{
    public async Task<Result<IEnumerable<FormDto>>> Handle(ListFormsQuery request, CancellationToken cancellationToken)
    {
        var pagingParams = new PagingParameters(request.Page, request.PageSize);
        var filterParams = CreateFilterParameters(request.FilterExpressions, request.FolderId);

        var spec = new FormsWithSubmissionsCountSpec(pagingParams, filterParams);
        IEnumerable<FormDto> forms = await repository.ListAsync(spec, cancellationToken);

        return Result.Success(forms);
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
