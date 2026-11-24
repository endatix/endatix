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
    /// <summary>Returns only outstanding actions applicable to the subject; an empty list means none.</summary>
    Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken);
}
