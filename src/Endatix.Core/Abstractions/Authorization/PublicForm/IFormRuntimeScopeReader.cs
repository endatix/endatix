namespace Endatix.Core.Abstractions.Authorization.PublicForm;

/// <summary>
/// What a form's public runtime may reach: the form's current state, and the data lists its definitions bind.
/// The only seam the public endpoints call, so how the references are found (analyzed on read today) can change
/// behind it.
/// </summary>
public interface IFormRuntimeScopeReader
{
    /// <summary>
    /// The current state of a form that exists and is not deleted, in any tenant; <c>null</c> otherwise. Read once
    /// per request, never from a shared cache, so a change of visibility, enabled state or deletion holds at once.
    /// </summary>
    Task<FormRuntimeScope?> GetAsync(long formId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the form's definitions bind the data list, within <paramref name="reach"/>. False for a form that
    /// does not exist or is deleted.
    /// </summary>
    Task<bool> ReachesDataListAsync(long formId, long dataListId, DataListReach reach, CancellationToken cancellationToken);
}
