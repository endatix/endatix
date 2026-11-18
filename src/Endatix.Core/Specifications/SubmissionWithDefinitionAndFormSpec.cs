using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specification for retrieving a form submission with its definition and form.
/// </summary>
public class SubmissionWithDefinitionAndFormSpec : SingleResultSpecification<Submission>
{
    public SubmissionWithDefinitionAndFormSpec(long formId, long submissionId)
        : this(formId, submissionId, includeTheme: false)
    {
    }

    /// <param name="formId">The form the submission belongs to.</param>
    /// <param name="submissionId">The submission.</param>
    /// <param name="includeTheme">Also loads the form's theme, for submission details.</param>
    public SubmissionWithDefinitionAndFormSpec(long formId, long submissionId, bool includeTheme)
    {
        Query.Include(s => s.FormDefinition);

        if (includeTheme)
        {
            Query.Include(s => s.Form).ThenInclude(f => f.Theme);
        }
        else
        {
            Query.Include(s => s.Form);
        }

        Query
            .Where(s => s.Id == submissionId && s.FormDefinition.FormId == formId)
            .AsNoTracking();
    }
}
