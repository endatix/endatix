using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.Personalization;

/// <summary>Makes a form's schema hold the audience columns a response carries, before the response is flattened.</summary>
internal interface IFormSchemaPersonalization
{
    /// <summary>
    /// Returns the schema to flatten with: <paramref name="schema"/> when it already has the columns, otherwise the
    /// schema saved with them.
    /// </summary>
    /// <param name="schema">The form's schema, as the flatten resolved it.</param>
    /// <param name="personalization">The snapshot frozen on the response; null when it has none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<PersonalizedSchema> IncludeAsync(
        Domain.FormSchema schema,
        PersonalizationRead? personalization,
        CancellationToken cancellationToken);
}

/// <summary>The schema to flatten with and its parsed flattening map, so the flatten does not parse it again.</summary>
internal sealed record PersonalizedSchema(Domain.FormSchema Schema, MergedFormSchema FlatteningMap)
{
    public static PersonalizedSchema Of(Domain.FormSchema schema) =>
        new(schema, FormSchemaFlatteningMap.FromJson(schema.FlatteningMap));
}
