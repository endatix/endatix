using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.DataLists.Search;

/// <summary>Searches a visible list for the authenticated tenant.</summary>
public sealed record SearchManagedDataListItems(SearchDataListItemsQuery Search) : IQuery<Result<Paged<DataListItemDto>>>;

internal sealed class SearchManagedDataListItemsHandler(DataListVisibility visibility, ISender sender)
    : IQueryHandler<SearchManagedDataListItems, Result<Paged<DataListItemDto>>>
{
    public async Task<Result<Paged<DataListItemDto>>> Handle(SearchManagedDataListItems request, CancellationToken cancellationToken)
    {
        return await visibility.ContainsAsync(request.Search.DataListId, cancellationToken)
            ? await sender.Send(request.Search, cancellationToken)
            : Result.NotFound("Data list not found.");
    }
}
