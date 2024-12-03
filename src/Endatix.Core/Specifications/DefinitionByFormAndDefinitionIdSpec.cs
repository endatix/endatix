using Ardalis.Specification;
using Endatix.Core.Entities;

public class DefinitionByFormAndDefinitionIdSpec : SingleResultSpecification<Form, FormDefinition?>
{
    public DefinitionByFormAndDefinitionIdSpec(long formId,long definitionId)
    {
        Query.Where(f => f.Id == formId && f.FormDefinitions.Any(fd => fd.Id == definitionId))
            .Include(f => f.FormDefinitions)
            .AsNoTracking();

        Query.Select(f => f.FormDefinitions.FirstOrDefault(fd => fd.Id == definitionId));
    }
}
