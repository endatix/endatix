using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Resolves the export schema for a form, compiling when missing or out of date.
/// </summary>
public interface IFormSchemaProvider
{
    /// <summary>
    /// Gets the export schema for a form, compiling when missing or out of date.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="formId">The ID of the form.</param>
    /// <param name="formDefinitionId">The ID of the form definition.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The export schema for the form, or null if the form is not found.</returns>
    Task<FormExportSchema?> GetOrCompileAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        CancellationToken cancellationToken);
}
