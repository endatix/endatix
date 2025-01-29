using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public sealed class FormWithDefinitionsAndSubmissionsSpec : Specification<Form>, ISingleResultSpecification<Form>
{
    public FormWithDefinitionsAndSubmissionsSpec(long formId)
    {
        Query
            .Where(f => f.Id == formId)
            .Include(f => f.FormDefinitions)
                .ThenInclude(fd => fd.Submissions);
    }
} 