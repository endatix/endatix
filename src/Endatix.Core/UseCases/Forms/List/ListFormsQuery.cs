using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.List;

/// <summary>
/// Query for listing forms with pagination, search, filters, sort, and date bounds.
/// </summary>
/// <param name="Page">The page number for pagination.</param>
/// <param name="PageSize">The number of items per page.</param>
/// <param name="Search">Optional name search (case-insensitive contains).</param>
/// <param name="IsEnabled">Optional enabled-state filter.</param>
/// <param name="IsPublic">Optional public-visibility filter.</param>
/// <param name="FilterExpressions">Optional facet filter expressions.</param>
/// <param name="FolderId">Optional folder scope.</param>
/// <param name="SortBy">Optional sort field. Defaults to <see cref="FormListSortBy.CreatedAt"/>.</param>
/// <param name="SortDescending">When true, sort descending (default).</param>
/// <param name="Created">Inclusive/exclusive UTC bounds for created-at.</param>
/// <param name="Modified">Inclusive/exclusive UTC bounds for modified-at.</param>
public record ListFormsQuery(
    int? Page,
    int? PageSize,
    string? Search = null,
    bool? IsEnabled = null,
    bool? IsPublic = null,
    IEnumerable<string>? FilterExpressions = null,
    long? FolderId = null,
    FormListSortBy SortBy = FormListSortBy.CreatedAt,
    bool SortDescending = true,
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default) : IQuery<Result<Paged<FormDto>>>;
