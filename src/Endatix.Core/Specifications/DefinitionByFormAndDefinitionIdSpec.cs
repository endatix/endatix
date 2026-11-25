using Ardalis.Specification;
using Endatix.Core.Entities;

#pragma warning disable S3903 // Public type left in global namespace: moving it would break NuGet consumers referencing the global name.
public class DefinitionByFormAndDefinitionIdSpec : SingleResultSpecification<Form, FormDefinition?>
{
    /// <summary>Selects the definition with the given id when it belongs to the given form.</summary>
    public DefinitionByFormAndDefinitionIdSpec(long formId, long definitionId) : this(formId, definitionId, null) { }

    internal DefinitionByFormAndDefinitionIdSpec(long formId, long definitionId, long? tenantId)
    {
        Query.Where(f => f.Id == formId && (!tenantId.HasValue || f.TenantId == tenantId)
                && f.FormDefinitions.Any(fd => fd.Id == definitionId && (!tenantId.HasValue || fd.TenantId == tenantId)))
            .Include(f => f.FormDefinitions)
            .AsNoTracking();

        Query.Select(f => f.FormDefinitions.FirstOrDefault(fd => fd.Id == definitionId));
    }
}
#pragma warning restore S3903
