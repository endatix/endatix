using Endatix.Api.Common;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.UseCases.FormDefinitions.List;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Request model for listing form definitions.
/// </summary>
public class FormDefinitionsListRequest :
    IPagedRequest,
    ISortableRequest<FormDefinitionListSortBy>,
    ICreatedRange,
    IModifiedRange
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

    /// <inheritdoc />
    public int? Page { get; set; }

    /// <inheritdoc />
    public int? PageSize { get; set; }

    /// <inheritdoc />
    public FormDefinitionListSortBy? SortBy { get; set; }

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
