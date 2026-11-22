using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Entities;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Reads and saves a tenant's branding and its versions.
/// </summary>
/// <remarks>
/// Two writes made from the same <see cref="TenantBranding.Version"/> must not both win. The database decides, not a
/// lock: the version token makes an update from an older version change no row, and the keys refuse a second first row
/// and a second version with the same number. The losing save is conceded instead of thrown, because Core cannot see
/// the provider's exceptions.
/// </remarks>
[Experimental("ENDATIX0002")]
public interface ITenantBrandingRepository
{
    /// <summary>The tenant's branding, tracked for a save; null when nothing was ever published.</summary>
    Task<TenantBranding?> FindAsync(long tenantId, CancellationToken cancellationToken);

    /// <summary>One of the tenant's versions, read only; null when it has no version with that number.</summary>
    Task<TenantBrandingVersion?> FindVersionAsync(long tenantId, long number, CancellationToken cancellationToken);

    /// <summary>
    /// Saves <paramref name="branding"/> (adding it when it is new) and its new <paramref name="version"/> in one save.
    /// </summary>
    /// <returns>True when saved; false when another write won the race. Nothing of the refused save stays tracked, so a
    /// later save in the same scope does not replay it.</returns>
    Task<bool> SaveOrConcedeAsync(TenantBranding branding, TenantBrandingVersion version, CancellationToken cancellationToken);
}
