using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;


/// <summary>
/// Returns all submissions for a given form ordered by CompletedAt Desc Includes all Form Definitions for that form
/// TODO: [check] add paging + additional filtering for the Submissions. e.g only for the Active Submission or only Completed. Evolve the code following the https://github.com/fullstackhero/dotnet-starter-kit and CleanArchitecture 
/// TODO: [check] We can add PagedSpecification<T> instead of Specification<T> as one direction to reuse and encapsulate logic, but we need to factor in other requirements like Basic filtering/sorting/ordering + specific filtering, e.g. only ActiveForms
/// TODO: [check] Also handle Ability to return PagedResult, which has the current page number and total count of items instead of basic list of results
/// </summary>
public class SubmissionsByFormIdSpec : Specification<Submission>
{
    public SubmissionsByFormIdSpec(long formId, PagingParameters pagingParams)
    {
        Query.Where(s => s.FormDefinition.FormId == formId)
            .OrderByDescending(s => s.CompletedAt)
            .Paginate(pagingParams)
            .AsNoTracking();
    }
}
