using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>The signed-in subject whose pending actions are being evaluated.</summary>
/// <param name="UserId">Canonical user identifier from authorization data.</param>
/// <param name="TenantId">Tenant in which the user is acting.</param>
/// <param name="Roles">Roles resolved for the user in that tenant.</param>
[Experimental("ENDATIX0006")]
public sealed record RequiredActionSubject(string UserId, long TenantId, IReadOnlyCollection<string> Roles);

/// <summary>Reports pending actions when authorization data is built for a signed-in user.</summary>
[Experimental("ENDATIX0006")]
public interface IRequiredActionProvider
{
    /// <summary>A stable, non-secret revision of the settings and behavior that determine this provider's reported actions.</summary>
    /// <remarks>Use the same revision on hosts with equivalent configuration. Change it when their snapshots are no
    /// longer interchangeable. Do not include credentials, customer identifiers, document text or raw settings.
    /// This is not a per-request key: changes in a subject's standing use normal access-version and expiry rules.</remarks>
    string CacheConfigurationKey { get; }

    /// <summary>Returns only outstanding actions applicable to the subject; an empty list means none.</summary>
    Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken);
}
