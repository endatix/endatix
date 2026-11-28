using Endatix.Core.Entities;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>Loads a tenant's form and saves its draft head and history together.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public interface IFormDraftRepository
{
    /// <summary>Reads the owning form and its active definition in the specified tenant.</summary>
    Task<Form?> FindFormAsync(long tenantId, long formId, CancellationToken cancellationToken);
    /// <summary>Whether the upgrade still needs to adopt this form's legacy draft.</summary>
    Task<bool> IsUpgradePendingAsync(long formId, CancellationToken cancellationToken);
    /// <summary>Reads the head tracked for a conditional save.</summary>
    Task<FormDraft?> FindHeadAsync(long formId, CancellationToken cancellationToken);
    /// <summary>Reads the ordinal of the current published definition, if one exists.</summary>
    Task<int?> ActiveNumberAsync(Form form, CancellationToken cancellationToken);
    /// <summary>Reads the actor at the requested version without reading its payload.</summary>
    Task<Actor?> LastActorAsync(long formId, long version, CancellationToken cancellationToken);
    /// <summary>Reads the most recent snapshot position for the periodic snapshot interval.</summary>
    Task<long> LatestSnapshotVersionAsync(long formId, long throughVersion, CancellationToken cancellationToken);
    /// <summary>Commits the head and every row together, returning false when another writer won.</summary>
    Task<bool> SaveOrConcedeAsync(FormDraft head, IReadOnlyList<FormDraftOperation> rows, CancellationToken cancellationToken);
}
