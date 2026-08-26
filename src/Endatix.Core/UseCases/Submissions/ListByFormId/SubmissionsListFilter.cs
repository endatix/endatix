using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Core.UseCases.Submissions.ListByFormId;

/// <summary>
/// Typed sort and calendar-day date bounds for submissions list / count specs.
/// </summary>
public sealed record SubmissionsListFilter(
    SubmissionListSortBy? SortBy = null,
    bool SortDescending = true,
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default,
    UtcDateTimeRange Started = default,
    UtcDateTimeRange Completed = default);
