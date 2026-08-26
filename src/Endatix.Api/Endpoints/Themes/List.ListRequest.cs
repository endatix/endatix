using Endatix.Api.Common;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.UseCases.Themes.List;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Request model for listing themes with optional pagination, sort, and date bounds.
/// </summary>
public class ListRequest :
    IPagedRequest,
    ISortableRequest<ThemeListSortBy>,
    ICreatedRange,
    IModifiedRange
{
    /// <inheritdoc />
    public int? Page { get; set; }

    /// <inheritdoc />
    public int? PageSize { get; set; }

    /// <inheritdoc />
    public ThemeListSortBy? SortBy { get; set; }

    /// <inheritdoc />
    public SortDirection? SortDir { get; set; }

    /// <inheritdoc />
    public string? CreatedFrom { get; set; }

    /// <inheritdoc />
    public string? CreatedTo { get; set; }

    /// <inheritdoc />
    public string? ModifiedFrom { get; set; }

    /// <inheritdoc />
    public string? ModifiedTo { get; set; }
}
