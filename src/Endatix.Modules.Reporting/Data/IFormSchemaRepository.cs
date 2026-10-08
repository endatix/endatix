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
    /// Takes the form's schema rebuild lock, then gets its compiled schema as last saved. The lock is held until the
    /// open transaction ends, so rebuilds of one form run one after another and each starts from the one before.
    /// </summary>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    /// <exception cref="Endatix.Infrastructure.Data.Locking.TransactionLockTimeoutException">
    /// Another rebuild of the form held the lock for longer than the wait allows.
    /// </exception>
    Task<FormSchema?> LockAndGetByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts or updates a compiled form schema.
    /// </summary>
    Task SaveAsync(FormSchema schema, CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes the compiled schema for a form (including soft-deleted rows).
    /// </summary>
    Task<int> DeleteByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken);
}
