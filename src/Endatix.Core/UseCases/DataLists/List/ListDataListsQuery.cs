using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Query to list data lists.
/// </summary>
/// <param name="Page">The page number for pagination.</param>
/// <param name="PageSize">The number of items per page for pagination.</param>
/// <param name="HasLocale">Optional locale code; filters parent rows whose AvailableLocales contain this code.</param>
/// <param name="Search">Optional name/description search (case-insensitive contains).</param>
public sealed record ListDataListsQuery(int? Page, int? PageSize, string? HasLocale = null, string? Search = null)
    : IQuery<Result<Paged<DataListDto>>>;
