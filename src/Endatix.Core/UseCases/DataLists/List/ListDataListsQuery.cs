using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Query to list data lists.
/// </summary>
/// <param name="Page">The page number for pagination.</param>
/// <param name="PageSize">The number of items per page for pagination.</param>
public sealed record ListDataListsQuery(int? Page, int? PageSize) : IQuery<Result<IEnumerable<DataListDto>>>;
