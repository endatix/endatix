using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for saved submissions grid views.
/// </summary>
public interface ISubmissionViewRepository
{
    /// <summary>Gets the form's shared view as saved, or <see langword="null"/> when nobody saved one.</summary>
    Task<SubmissionView?> FindSharedAsync(long tenantId, long formId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the form's shared view when <see cref="SubmissionViewSave.Revision"/> is the one saved last
    /// (<see cref="SubmissionView.UnsavedRevision"/> when there is none yet).
    /// </summary>
    /// <returns>The saved view, or <see langword="null"/> when another save got there first.</returns>
    Task<SubmissionView?> SaveSharedAsync(SubmissionViewSave save, CancellationToken cancellationToken);

    /// <summary>Hard-deletes every view of a form, shared and personal.</summary>
    Task<int> DeleteByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken);
}

/// <summary>A save of a form's shared view, made from the view read at <paramref name="Revision"/>.</summary>
public sealed record SubmissionViewSave(
    long TenantId,
    long FormId,
    long Revision,
    string SettingsJson,
    long? ModifiedBy);
