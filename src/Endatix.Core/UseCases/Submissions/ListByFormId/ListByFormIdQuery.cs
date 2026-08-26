using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.ListByFormId;

/// <summary>
/// Query for listing submissions for a form with pagination, facet filters, sort, and date bounds.
/// </summary>
public record ListByFormIdQuery(
    long FormId,
    int? Page,
    int? PageSize,
    IEnumerable<string>? FilterExpressions = null,
    SubmissionListSortBy? SortBy = null,
    bool SortDescending = true,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    DateTime? ModifiedFrom = null,
    DateTime? ModifiedTo = null,
    DateTime? StartedFrom = null,
    DateTime? StartedTo = null,
    DateTime? CompletedFrom = null,
    DateTime? CompletedTo = null) : IQuery<Result<Paged<SubmissionDto>>>;
