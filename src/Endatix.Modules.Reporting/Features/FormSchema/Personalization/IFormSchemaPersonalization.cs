using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.Personalization;

/// <summary>
/// Makes a form's schema hold the audience columns a response carries and the configured submitter profile columns,
/// before the response is flattened.
/// </summary>
internal interface IFormSchemaPersonalization
{
    /// <summary>
    /// Returns the parsed flattening map to flatten with: <paramref name="schema"/>'s when it already has the columns,
    /// or cannot take them, otherwise the one saved with them.
    /// </summary>
    /// <param name="schema">The form's schema, as the flatten resolved it.</param>
    /// <param name="personalization">The snapshot frozen on the response; null when it has none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<MergedFormSchema> IncludeAsync(
        Domain.FormSchema schema,
        PersonalizationRead? personalization,
        CancellationToken cancellationToken);
}

