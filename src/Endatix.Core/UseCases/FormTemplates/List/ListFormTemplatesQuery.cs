using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.List;

/// <summary>
/// Query for listing form templates with pagination, filters, sort, and date bounds.
/// </summary>
/// <param name="Page">The page number for pagination.</param>
/// <param name="PageSize">The number of items per page.</param>
/// <param name="FilterExpressions">Optional facet filter expressions.</param>
/// <param name="FolderId">Optional folder scope.</param>
/// <param name="SortBy">Optional sort field. Defaults to <see cref="FormTemplateListSortBy.CreatedAt"/>.</param>
/// <param name="SortDescending">When true, sort descending (default).</param>
/// <param name="Created">Inclusive/exclusive UTC bounds for created-at.</param>
/// <param name="Modified">Inclusive/exclusive UTC bounds for modified-at.</param>
public record ListFormTemplatesQuery(
    int? Page,
    int? PageSize,
    IEnumerable<string>? FilterExpressions = null,
    long? FolderId = null,
    FormTemplateListSortBy SortBy = FormTemplateListSortBy.CreatedAt,
    bool SortDescending = true,
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default)
    : IQuery<Result<IEnumerable<FormTemplateDto>>>;
