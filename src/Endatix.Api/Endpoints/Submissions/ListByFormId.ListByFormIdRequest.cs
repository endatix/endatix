using Endatix.Api.Common;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.UseCases.Submissions.ListByFormId;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request object to get list of submissions for a given form
/// </summary>
public class ListByFormIdRequest :
    IPagedRequest,
    ISortableRequest<SubmissionListSortBy>,
    IFilterable,
    ICreatedRange,
    IModifiedRange,
    IStartedRange,
    ICompletedRange
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
    public SubmissionListSortBy? SortBy { get; set; }

    /// <inheritdoc />
    public SortDirection? SortDir { get; set; }

    /// <inheritdoc />
    public IEnumerable<string>? Filter { get; set; }

    /// <inheritdoc />
    public string? CreatedFrom { get; set; }

    /// <inheritdoc />
    public string? CreatedTo { get; set; }

    /// <inheritdoc />
    public string? ModifiedFrom { get; set; }

    /// <inheritdoc />
    public string? ModifiedTo { get; set; }

    /// <inheritdoc />
    public string? StartedFrom { get; set; }

    /// <inheritdoc />
    public string? StartedTo { get; set; }

    /// <inheritdoc />
    public string? CompletedFrom { get; set; }

    /// <inheritdoc />
    public string? CompletedTo { get; set; }

    /// <summary>
    /// The list query this request asks for. The default sort is newest first.
    /// </summary>
    public ListByFormIdQuery ToQuery()
    {
        var sort = this.ToNullableSortRequest(SubmissionListSortBy.CreatedAt, SortDirection.Desc);
        return new ListByFormIdQuery(
            FormId,
            Page,
            PageSize,
            Filter,
            sort?.Field,
            sort?.IsDescending ?? false,
            this.ToCreatedRange(),
            this.ToModifiedRange(),
            this.ToStartedRange(),
            this.ToCompletedRange());
    }
}
