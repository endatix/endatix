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
[DirectCallOnly]
public sealed class ListDataListsHandler(IRepository<DataList> repository)
    : IQueryHandler<ListDataListsQuery, Result<Paged<DataListDto>>>, IExplicitTenantBinding
{
    private readonly ITenantContext? _tenant;
    bool IExplicitTenantBinding.IsTenantBound => _tenant is not null;

    /// <summary>The managed handler requires a tenant; direct repository-only callers supply their own scoped repository.</summary>
    public ListDataListsHandler(IRepository<DataList> repository, ITenantContext tenant) : this(repository) =>
        _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));

    /// <inheritdoc />
    public async Task<Result<Paged<DataListDto>>> Handle(ListDataListsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenant?.TenantId ?? request.TenantId;
        if (tenantId <= 0 || (request.TenantId.HasValue && request.TenantId != tenantId))
        {
            return Result.Unauthorized("Tenant context is required.");
        }
        PagingParameters pagingParams = new(request.Page, request.PageSize);
        var filter = new DataListsSpecifications.ListFilter(request.HasLocale, request.Search, request.SortBy,
            request.SortDescending, request.Created, request.Modified)
        { TenantId = tenantId };
        var listSpec = new DataListsSpecifications.ListSpec(filter);
        var totalRecords = await repository.CountAsync(listSpec, cancellationToken);
        var resolved = pagingParams.ForTotal(totalRecords);

        var pagedSpec = new DataListsSpecifications.ListWithPagingToDtoSpec(new PagingParameters(resolved), filter);
        IReadOnlyList<DataListDto> items = totalRecords > 0 ? await repository.ListAsync(pagedSpec, cancellationToken) : [];
        return Result.Success(resolved.ToPaged(items));
    }

    internal sealed class Registered(IRepository<DataList> repository, ITenantContext tenant)
        : RegisteredContentHandler<ListDataListsQuery, Result<Paged<DataListDto>>>(new ListDataListsHandler(repository, tenant));
}
