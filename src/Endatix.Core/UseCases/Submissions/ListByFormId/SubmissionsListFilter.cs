namespace Endatix.Core.UseCases.Submissions.ListByFormId;

/// <summary>
/// Typed sort and calendar-day date bounds for submissions list / count specs.
/// </summary>
public sealed record SubmissionsListFilter(
    SubmissionListSortBy? SortBy = null,
    bool SortDescending = true,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    DateTime? ModifiedFrom = null,
    DateTime? ModifiedTo = null,
    DateTime? StartedFrom = null,
    DateTime? StartedTo = null,
    DateTime? CompletedFrom = null,
    DateTime? CompletedTo = null);
