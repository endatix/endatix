using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Handler for listing data lists.
/// </summary>
public sealed class ListDataListsHandler(IRepository<DataList> repository, ITenantContext? tenant)
    : IQueryHandler<ListDataListsQuery, Result<Paged<DataListDto>>>
{
    /// <summary>Uses the query's tenant when constructed without a request scope.</summary>
    public ListDataListsHandler(IRepository<DataList> repository) : this(repository, null) { }

    /// <inheritdoc />
    public async Task<Result<Paged<DataListDto>>> Handle(ListDataListsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? tenant?.TenantId ?? 0;
        if (tenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }
        PagingParameters pagingParams = new(request.Page, request.PageSize);
        var filter = new DataListsSpecifications.ListFilter(request.HasLocale, request.Search, request.SortBy,
            request.SortDescending, request.Created, request.Modified);
        var listSpec = new DataListsSpecifications.ListSpec(filter);
        listSpec.Query.Where(list => list.TenantId == tenantId);
        var totalRecords = await repository.CountAsync(listSpec, cancellationToken);
        var resolved = pagingParams.ForTotal(totalRecords);

        var pagedSpec = new DataListsSpecifications.ListWithPagingToDtoSpec(new PagingParameters(resolved), filter);
        pagedSpec.Query.Where(list => list.TenantId == tenantId);
        IReadOnlyList<DataListDto> items = totalRecords > 0 ? await repository.ListAsync(pagedSpec, cancellationToken) : [];
        return Result.Success(resolved.ToPaged(items));
    }
}
