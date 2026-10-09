namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Compiles and persists the export schema for a form definition (outbox worker entry point).
/// </summary>
public interface IFormSchemaProcessor
{
    /// <summary>
    /// Compiles and persists the export schema for a form definition, after the definition changed.
    /// </summary>
    /// <remarks>
    /// A definition older than the schema only adds the columns the schema lacks, and only when the form has real
    /// submissions, unless <paramref name="replace"/> is set. Without real submissions the newer definition's rebuild
    /// replaced the schema, and merging the older one would bring back what it removed.
    /// </remarks>
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

    /// <summary>
    /// Makes the export schema hold a definition's columns, before a submission made on it is flattened.
    /// </summary>
    /// <remarks>
    /// Compiles only when the schema is older than the definition or lacks one of its columns, and then never drops
    /// the columns of a newer definition.
    /// </remarks>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="formId">The ID of the form.</param>
    /// <param name="formDefinitionId">The ID of the definition the submission was made on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task IncludeDefinitionAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        CancellationToken cancellationToken = default);
}
