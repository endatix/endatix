using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Filters;

namespace Endatix.Core.Specifications;

public sealed class FormDefinitionsByFormIdSpec : Specification<FormDefinition>
{
    public FormDefinitionsByFormIdSpec(long formId, PagingFilter filter = null)
    {
        Query
            .Where(fd => fd.FormId == formId)
            .Paginate(filter)
            .AsNoTracking();
    }
}
