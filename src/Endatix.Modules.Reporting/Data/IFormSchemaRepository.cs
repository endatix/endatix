using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for compiled form schemas.
/// </summary>
public interface IFormSchemaRepository
{
    /// <summary>
    /// Gets the compiled schema for a form.
    /// </summary>
    Task<FormSchema?> GetByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts or updates a compiled form schema.
    /// </summary>
    Task SaveAsync(FormSchema schema, CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes the compiled schema for a form (including soft-deleted rows).
    /// </summary>
    Task<int> DeleteByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken);
}
