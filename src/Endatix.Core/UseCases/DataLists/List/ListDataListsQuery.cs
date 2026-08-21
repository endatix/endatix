using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Query to list data lists.
/// </summary>
/// <param name="Page">The page number for pagination.</param>
/// <param name="PageSize">The number of items per page for pagination.</param>
/// <param name="HasLocale">
/// Optional culture code or comma-separated list (e.g. <c>es</c> or <c>es,de</c>).
/// Matches lists whose <c>AvailableLocales</c> contain any code or whose <c>DefaultLocale</c> equals any code.
/// </param>
/// <param name="Query">Optional name/description search (case-insensitive contains).</param>
public sealed record ListDataListsQuery(int? Page, int? PageSize, string? HasLocale = null, string? Query = null)
    : IQuery<Result<Paged<DataListDto>>>;
