using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

public sealed class FormDefinitionsByFormIdSpec : Specification<FormDefinition>
{
    public FormDefinitionsByFormIdSpec(long formId, PagingParameters? pagingParams = null)
    {
        Query
            .Where(fd => fd.FormId == formId)
            .Paginate(pagingParams!)
            .AsNoTracking();
    }
}
