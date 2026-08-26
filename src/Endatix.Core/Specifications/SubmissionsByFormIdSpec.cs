using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.ListByFormId;

namespace Endatix.Core.Specifications;

/// <summary>
/// Returns submissions for a given form with facet filters, typed date bounds, sort, and paging.
/// </summary>
public class SubmissionsByFormIdSpec : SubmissionsByFormIdFilteredSpecBase<SubmissionDto>
{
    /// <summary>
    /// Initializes a new instance of the specification to retrieve submissions for a given form.
    /// </summary>
    public SubmissionsByFormIdSpec(
        long formId,
        PagingParameters pagingParams,
        FilterParameters filterParams,
        SubmissionsListFilter? listFilter = null)
        : base(filterParams)
    {
        listFilter ??= new SubmissionsListFilter();

        Query
            .WhereFormIdAndFilters(formId, filterParams)
            .ApplyListDateRanges(listFilter);

        ApplyListOrdering(Query, listFilter);

        Query
            .Paginate(pagingParams)
            .AsNoTracking();

        Query.Select(s => new SubmissionDto(
            s.Id,
            s.IsComplete,
            s.JsonData,
            s.FormId,
            s.FormDefinitionId,
            s.CurrentPage,
            s.CompletedAt,
            s.StartedAt,
            s.CreatedAt,
            s.ModifiedAt,
            s.Metadata,
            s.Status.Code,
            s.SubmittedBy,
            s.SubmitterId,
            s.SubmitterDisplayId,
            s.SubmitterProfileSnapshot,
            s.IsTestSubmission
        ));
    }

    internal static void ApplyListOrdering(
        ISpecificationBuilder<Submission> query,
        SubmissionsListFilter listFilter)
    {
        // Preserve v1 default when sort is omitted: CreatedAt desc, then CompletedAt desc.
        if (listFilter.SortBy is null)
        {
            query.OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.CompletedAt);
            return;
        }

        switch (listFilter.SortBy.Value)
        {
            case SubmissionListSortBy.ModifiedAt:
                query.OrderByWithIdTiebreaker(x => x.ModifiedAt, listFilter.SortDescending);
                break;
            case SubmissionListSortBy.StartedAt:
                query.OrderByWithIdTiebreaker(x => x.StartedAt, listFilter.SortDescending);
                break;
            case SubmissionListSortBy.CompletedAt:
                query.OrderByWithIdTiebreaker(x => x.CompletedAt, listFilter.SortDescending);
                break;
            case SubmissionListSortBy.Id when listFilter.SortDescending:
                query.OrderByDescending(x => x.Id);
                break;
            case SubmissionListSortBy.Id:
                query.OrderBy(x => x.Id);
                break;
            default:
                query.OrderByWithIdTiebreaker(x => x.CreatedAt, listFilter.SortDescending);
                break;
        }
    }
}
