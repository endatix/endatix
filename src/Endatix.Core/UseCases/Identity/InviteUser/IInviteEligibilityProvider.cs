namespace Endatix.Core.UseCases.Identity.InviteUser;

/// <summary>Optional module check for the current caller inviting a teammate in a tenant.</summary>
public interface IInviteEligibilityProvider
{
    /// <summary>Decides eligibility after permission and role checks, without registering or notifying the invitee.</summary>
    Task<InviteEligibility> DecideAsync(long tenantId, CancellationToken cancellationToken);
}

/// <summary>Only an affirmative module decision permits registration.</summary>
public enum InviteEligibility
{
    Unavailable,
    Allowed,
    Declined
}
