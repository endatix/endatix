using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;


/// <summary>
/// Returns the total count of submissions per form.
/// </summary>
public class SubmissionsTotalCountByFormDefinitionIdSpec : Specification<Submission>
{
    /// <summary>
    /// Initializes a new instance of the specification to retrieve submissions for a given form definition
    /// </summary>
    /// <param name="formDefinitionId">The ID of the form definition to retrieve submissions for</param>
    public SubmissionsTotalCountByFormDefinitionIdSpec(long formDefinitionId)
    {
        Query.Where(s => s.FormDefinition.Id == formDefinitionId)
            .AsNoTracking();
    }
}