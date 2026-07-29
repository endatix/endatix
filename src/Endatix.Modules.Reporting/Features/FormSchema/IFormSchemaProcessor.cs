namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Compiles and persists the export schema for a form definition (outbox worker entry point).
/// </summary>
public interface IFormSchemaProcessor
{
    /// <summary>
    /// Compiles and persists the export schema for a form definition.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="formId">The ID of the form.</param>
    /// <param name="formDefinitionId">The ID of the form definition.</param>
    /// <param name="replace">
    /// When <c>true</c>, always replace the schema and clear flattened rows even if real submissions exist.
    /// When <c>false</c> (default), replace only when the form has no real submissions; otherwise merge.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        bool replace = false,
        CancellationToken cancellationToken = default);
}
