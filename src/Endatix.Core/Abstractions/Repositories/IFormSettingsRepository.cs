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
    /// <summary>The form's settings, read only; a row at its defaults when none is stored.</summary>
    /// <remarks>The tenant query filter scopes the read; <paramref name="tenantId"/> only owns the defaults row.</remarks>
    Task<FormSettings> ReadAsync(long tenantId, long formId, CancellationToken cancellationToken);

    /// <summary>The form's settings, tracked for a save; null when none are stored.</summary>
    Task<FormSettings?> FindAsync(long formId, CancellationToken cancellationToken);

    /// <summary>Saves <paramref name="settings"/>, adding the row when it is new.</summary>
    /// <returns>True when saved; false when another write won the race. Nothing of the refused save stays tracked, so a
    /// later save in the same scope does not replay it.</returns>
    Task<bool> SaveOrConcedeAsync(FormSettings settings, CancellationToken cancellationToken);
}
