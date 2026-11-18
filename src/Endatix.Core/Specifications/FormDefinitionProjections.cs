using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Projection specifications for reading form definitions without loading them, in any tenant: callers check the
/// form's tenant first.
/// </summary>
public static class FormDefinitionProjections
{
    /// <summary>A definition and the time it was last saved, which changes whenever its JSON does.</summary>
    public sealed record FormDefinitionVersionDto(long Id, DateTime Version);

    /// <summary>The id and version of every definition of a form that is not deleted.</summary>
    public sealed class VersionsByFormIdSpec : Specification<FormDefinition, FormDefinitionVersionDto>
    {
        /// <param name="formId">The form whose definitions to list.</param>
        public VersionsByFormIdSpec(long formId)
        {
            Query
                .IgnoreQueryFilters()
                .Where(definition => definition.FormId == formId && !definition.IsDeleted)
                .AsNoTracking();

            Query.Select(definition => new FormDefinitionVersionDto(
                definition.Id,
                definition.ModifiedAt ?? definition.CreatedAt));
        }
    }

    /// <summary>The JSON of one definition that is not deleted.</summary>
    public sealed class JsonByIdSpec : Specification<FormDefinition, string?>, ISingleResultSpecification<FormDefinition, string?>
    {
        /// <param name="definitionId">The definition to read.</param>
        public JsonByIdSpec(long definitionId)
        {
            DefinitionId = definitionId;
            Query
                .IgnoreQueryFilters()
                .Where(definition => definition.Id == definitionId && !definition.IsDeleted)
                .AsNoTracking();

            Query.Select(definition => definition.JsonData);
        }

        /// <summary>The definition this specification reads.</summary>
        public long DefinitionId { get; }
    }
}
