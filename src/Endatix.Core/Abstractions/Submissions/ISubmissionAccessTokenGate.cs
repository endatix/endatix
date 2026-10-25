namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Whether a valid submission access token still holds for its submission: a signed token grants its claims until
/// it expires, unless what it was issued for has gone. The default holds every token. The audience module refuses
/// the token of a personalised link that was reissued, or whose person left the form.
/// </summary>
public interface ISubmissionAccessTokenGate
{
    /// <summary><see langword="false"/> once what <paramref name="claims"/> were issued for has gone.</summary>
    Task<bool> IsCurrentAsync(SubmissionAccessTokenClaims claims, CancellationToken cancellationToken);
}
