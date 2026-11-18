using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specification for retrieving a form submission with its definition and form.
/// </summary>
public class SubmissionWithDefinitionAndFormSpec : SingleResultSpecification<Submission>
{
    public SubmissionWithDefinitionAndFormSpec(long formId, long submissionId)
    {
        Query
            .Include(s => s.FormDefinition)
            .Include(s => s.Form)
            .Where(s => s.Id == submissionId && s.FormDefinition.FormId == formId)
            .AsNoTracking();
    }
}
