using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Resolves the compiled form schema, compiling when missing or out of date.
/// </summary>
public interface IFormSchemaProvider
{
    /// <summary>
    /// Gets the compiled schema for a form, compiling when missing or out of date.
    /// </summary>
    Task<Domain.FormSchema?> GetOrCompileAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        CancellationToken cancellationToken);
}
