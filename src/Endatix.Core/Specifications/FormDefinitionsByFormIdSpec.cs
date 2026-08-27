using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// All definitions for a form (existence checks; no paging).
/// </summary>
public sealed class FormDefinitionsByFormIdSpec : Specification<FormDefinition>
{
    public FormDefinitionsByFormIdSpec(long formId)
    {
        Query
            .Where(fd => fd.FormId == formId)
            .AsNoTracking();
    }
}
