using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// One form with its definitions, tracked. The submissions are left out: there can be any number of them.
/// </summary>
public sealed class FormWithDefinitionsSpec : Specification<Form>, ISingleResultSpecification<Form>
{
    public FormWithDefinitionsSpec(long formId)
    {
        Query
            .Where(f => f.Id == formId)
            .Include(f => f.FormDefinitions);
    }
}
