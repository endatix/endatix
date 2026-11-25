using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>Retires authorization snapshots after a module commits a change to a standing input.</summary>
[Experimental("ENDATIX0006")]
public interface IAuthorizationDataChanges
{
    /// <summary>Refresh every user of this tenant on their next request, on every host.</summary>
    Task TenantChangedAsync(long tenantId, CancellationToken cancellationToken);

    /// <summary>Refresh authorization data for every tenant.</summary>
    Task AllChangedAsync(CancellationToken cancellationToken);
}
