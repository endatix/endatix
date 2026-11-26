using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Entities;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Reads and saves a form's settings.
/// </summary>
/// <remarks>
/// Two writes made from the same <see cref="FormSettings.Version"/> must not both win. The database decides, not a lock:
/// the version token makes an update from an older version change no row, and the key refuses a second first row. The
/// losing save is conceded instead of thrown, because Core cannot see the provider's exceptions.
/// </remarks>
[Experimental("ENDATIX0005")]
public interface IFormSettingsRepository
{
    /// <summary><paramref name="form"/>'s settings, read only; <see cref="FormSettings.For"/> the form when none are
    /// stored.</summary>
    /// <remarks>Scoped to the form's tenant as well as by the tenant query filter, which is off for an anonymous
    /// share-link read.</remarks>
    Task<FormSettings> ReadAsync(Form form, CancellationToken cancellationToken);

    /// <summary>The form's settings, tracked for a save; null when none are stored.</summary>
    Task<FormSettings?> FindAsync(long formId, CancellationToken cancellationToken);

    /// <summary>Saves <paramref name="settings"/>, adding the row when it is new.</summary>
    /// <returns>True when saved; false when another write won the race. Nothing of the refused save stays tracked, so a
    /// later save in the same scope does not replay it.</returns>
    Task<bool> SaveOrConcedeAsync(FormSettings settings, CancellationToken cancellationToken);

    /// <summary>
    /// Updates <paramref name="form"/>. While a form's webhook settings are kept in both places (endatix/endatix#1784), a
    /// save that changes them writes the form's settings too, so it can lose to another form save or a settings write
    /// on the same form.
    /// </summary>
    /// <returns>True when saved; false when it lost that race. The whole save rolled back, and nothing of it stays
    /// tracked, so a later save in the same scope does not replay it.</returns>
    Task<bool> UpdateFormOrConcedeAsync(Form form, CancellationToken cancellationToken);
}
