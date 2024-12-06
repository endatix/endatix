using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications; 

public sealed class ActiveFormDefinitionByFormIdSpec : Specification<Form>, ISingleResultSpecification<Form>
{
    public ActiveFormDefinitionByFormIdSpec(long formId)
    {
        Query.Where(f => f.Id == formId)
             .Include(f => f.ActiveDefinition);
    }
}
