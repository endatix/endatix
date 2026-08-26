using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
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
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default,
    UtcDateTimeRange Started = default,
    UtcDateTimeRange Completed = default) : IQuery<Result<Paged<SubmissionDto>>>;
