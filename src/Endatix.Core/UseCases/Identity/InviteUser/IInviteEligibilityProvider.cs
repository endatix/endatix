using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.UseCases.Identity.InviteUser;

/// <summary>Optional module check for the current caller inviting a teammate in a tenant.</summary>
[Experimental("ENDATIX0007")]
public interface IInviteEligibilityProvider
{
    /// <summary>Decides eligibility after permission and role checks, without registering or notifying the invitee.</summary>
    Task<InviteEligibility> DecideAsync(long tenantId, CancellationToken cancellationToken);
}

/// <summary>Only an affirmative module decision permits registration.</summary>
[Experimental("ENDATIX0007")]
public enum InviteEligibility
{
    /// <summary>The provider could not determine eligibility. Retry later.</summary>
    Unavailable,
    /// <summary>Proceed with registration.</summary>
    Allowed,
    /// <summary>The workspace is known to be ineligible.</summary>
    Declined
}
