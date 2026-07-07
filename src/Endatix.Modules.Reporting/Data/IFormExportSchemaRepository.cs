using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for form export schemas.
/// </summary>
public interface IFormExportSchemaRepository
{
    /// <summary>
    /// Gets a form export schema by form ID.
    /// </summary>
    Task<FormExportSchema?> GetByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a form export schema (insert when new, update when tracked).
    /// </summary>
    Task SaveAsync(FormExportSchema schema, CancellationToken cancellationToken);
}
