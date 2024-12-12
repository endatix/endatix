using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;


/// <summary>
/// Returns all submissions for a given form ordered by CompletedAt Desc. Includes all Form Definitions for that form.
/// TODO: [check] add paging + additional filtering for the Submissions. e.g only for the Active Submission or only Completed. Evolve the code following the https://github.com/fullstackhero/dotnet-starter-kit and CleanArchitecture 
/// TODO: [check] We can add PagedSpecification&lt;T&gt; instead of Specification&lt;T&gt; as one direction to reuse and encapsulate logic, but we need to factor in other requirements like Basic filtering/sorting/ordering + specific filtering, e.g. only ActiveForms
/// TODO: [check] Also handle Ability to return PagedResult, which has the current page number and total count of items instead of basic list of results
/// </summary>
public class SubmissionsByFormIdSpec : Specification<Submission>
{
    /// <summary>
    /// Initializes a new instance of the specification to retrieve submissions for a given form
    /// </summary>
    /// <param name="formId">The ID of the form to retrieve submissions for</param>
    /// <param name="pagingParams">Parameters for paging the results</param>
    /// <param name="filterParams">Parameters for filtering the results</param>
    public SubmissionsByFormIdSpec(long formId, PagingParameters pagingParams, FilterParameters filterParams)
    {
        Query.Where(s => s.FormDefinition.FormId == formId)
            .Filter(filterParams)
            .OrderByDescending(s => s.CompletedAt)
            .Paginate(pagingParams)
            .AsNoTracking();
    }
}
