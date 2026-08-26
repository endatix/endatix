using Endatix.Api.Common;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.UseCases.FormTemplates.List;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Request model for listing form templates.
/// </summary>
public class FormTemplatesListRequest :
    IPagedRequest,
    IFilterable,
    ISortableRequest<FormTemplateListSortBy>,
    ICreatedRange,
    IModifiedRange
{
    /// <summary>
    /// The number of the page
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// The number of items to take.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// The filter expressions.
    /// </summary>
    public IEnumerable<string>? Filter { get; set; }

    /// <summary>
    /// Optional folder filter.
    /// </summary>
    public long? FolderId { get; set; }

    /// <inheritdoc />
    public FormTemplateListSortBy? SortBy { get; set; }

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
