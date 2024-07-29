using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications; 

public sealed class ActiveFormDefinitionByFormIdSpec : Specification<FormDefinition>, ISingleResultSpecification<FormDefinition>
{
    public ActiveFormDefinitionByFormIdSpec(long formId)
    {
        Query.Where(fd => fd.FormId == formId && fd.IsActive);
    }
}
