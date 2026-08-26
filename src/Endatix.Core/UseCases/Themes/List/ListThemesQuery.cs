using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.List;

/// <summary>
/// Query for retrieving themes with pagination, sort, and date bounds.
/// </summary>
/// <param name="Page">Optional page number for pagination.</param>
/// <param name="PageSize">Optional page size for pagination.</param>
/// <param name="SortBy">Optional sort field. Defaults to <see cref="ThemeListSortBy.ModifiedAt"/>.</param>
/// <param name="SortDescending">When true, sort descending (default).</param>
/// <param name="CreatedFrom">Inclusive UTC start of created-at day filter.</param>
/// <param name="CreatedTo">Exclusive UTC end of created-at day filter.</param>
/// <param name="ModifiedFrom">Inclusive UTC start of modified-at day filter.</param>
/// <param name="ModifiedTo">Exclusive UTC end of modified-at day filter.</param>
public record ListThemesQuery(
    int? Page = null,
    int? PageSize = null,
    ThemeListSortBy SortBy = ThemeListSortBy.ModifiedAt,
    bool SortDescending = true,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    DateTime? ModifiedFrom = null,
    DateTime? ModifiedTo = null)
    : IQuery<Result<IEnumerable<Theme>>>;
