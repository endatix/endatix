using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.List;

/// <summary>
/// Query for retrieving themes with pagination, sort, and date bounds.
/// </summary>
/// <param name="Page">Optional page number for pagination.</param>
/// <param name="PageSize">Optional page size for pagination.</param>
/// <param name="SortBy">Optional sort field. Defaults to <see cref="ThemeListSortBy.ModifiedAt"/>.</param>
/// <param name="SortDescending">When true, sort descending (default).</param>
/// <param name="Created">Inclusive/exclusive UTC bounds for created-at.</param>
/// <param name="Modified">Inclusive/exclusive UTC bounds for modified-at.</param>
public record ListThemesQuery(
    int? Page = null,
    int? PageSize = null,
    ThemeListSortBy SortBy = ThemeListSortBy.ModifiedAt,
    bool SortDescending = true,
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default)
    : IQuery<Result<IEnumerable<Theme>>>;
