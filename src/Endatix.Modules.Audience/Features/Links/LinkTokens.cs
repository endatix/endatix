using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// The tokens of a link open. It hands out a signed access token to fill in the link's one submission, whatever the
/// form's privacy, for the form's response-token lifetime, bound to the link's <see cref="AudienceLink.AccessStamp"/>.
/// </summary>
internal sealed class LinkTokens(
    ISubmissionAccessTokenService accessTokens,
    ISubmissionTokenService submissionTokens)
{
    private static readonly string[] _fillIn = [SubmissionAccessTokenPermissions.Submit.Name];

    /// <summary>
    /// A signed token always expires: a lifetime that never ends, and one past
    /// <see cref="ISubmissionAccessTokenService.MAX_EXPIRY_MINUTES"/>, get that cap. Revocation runs through the gate,
    /// and an open link can be redeemed again. A tenant with no settings gets
    /// <see cref="TenantSettings.DefaultSubmissionTokenExpiryHours"/>, as its submission tokens do.
    /// </summary>
    public async Task<Result<SubmissionAccessTokenDto>> IssueAccessTokenAsync(
        AudienceLink link,
        long submissionId,
        CancellationToken cancellationToken)
    {
        var hours = await submissionTokens.GetExpiryHoursAsync(link.FormId, link.TenantId, cancellationToken);
        var minutes = (int)Math.Min(
            (long?)hours * 60 ?? ISubmissionAccessTokenService.MAX_EXPIRY_MINUTES,
            ISubmissionAccessTokenService.MAX_EXPIRY_MINUTES);
        return accessTokens.GenerateAccessToken(
            new SubmissionAccessTokenRequest(submissionId, minutes, _fillIn, link.AccessStamp()));
    }
}
