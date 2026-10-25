using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// The tokens of a link open. It hands out a signed access token to fill in the link's one submission, whatever the
/// form's privacy, for the form's response-token lifetime, bound to the link's <see cref="AudienceLink.AccessStamp"/>.
/// The submission's own token is still issued under the link's lock and handed out beside it.
/// </summary>
internal sealed class LinkTokens(
    ISubmissionAccessTokenService accessTokens,
    ISubmissionTokenService submissionTokens)
{
    private static readonly string[] _fillIn = [SubmissionAccessTokenPermissions.Submit.Name];

    public Task<Result<string>> ObtainSubmissionTokenAsync(long submissionId, CancellationToken cancellationToken) =>
        submissionTokens.ObtainTokenAsync(submissionId, cancellationToken);

    /// <summary>
    /// A lifetime that never ends gets <see cref="TenantSettings.DefaultSubmissionTokenExpiryHours"/>, the one a
    /// submission token gets when its tenant has no settings: a link can be redeemed again at any time. One past
    /// <see cref="ISubmissionAccessTokenService.MaxExpiryMinutes"/> gets that cap.
    /// </summary>
    public async Task<Result<SubmissionAccessTokenDto>> IssueAccessTokenAsync(
        AudienceLink link,
        long submissionId,
        CancellationToken cancellationToken)
    {
        var hours = await submissionTokens.GetExpiryHoursAsync(link.FormId, link.TenantId, cancellationToken);
        var minutes = (int)Math.Min(
            (long)(hours ?? TenantSettings.DefaultSubmissionTokenExpiryHours) * 60,
            ISubmissionAccessTokenService.MaxExpiryMinutes);
        return accessTokens.GenerateAccessToken(
            new SubmissionAccessTokenRequest(submissionId, minutes, _fillIn, link.AccessStamp()));
    }
}

/// <summary>
/// A link's access token holds while its link still names its submission with the same stamp and the person is
/// still on the form: a reissue or a removal refuses it within <see cref="_checkLifetime"/>. Tokens without a stamp
/// were not issued by a link and always hold. The reads lift the tenant filter, so the cached answer is the same for
/// every caller: a signed-in user of another tenant must not cache a refusal for the participant.
/// </summary>
internal sealed class LinkAccessTokenGate(IAudienceDbContext db, LinkOpenGates gates, HybridCache cache)
    : ISubmissionAccessTokenGate
{
    private static readonly TimeSpan _checkLifetime = TimeSpan.FromMinutes(1);

    private static readonly HybridCacheEntryOptions _cacheOptions = new()
    {
        Expiration = _checkLifetime,
        LocalCacheExpiration = _checkLifetime,
    };

    public async Task<bool> IsCurrentAsync(SubmissionAccessTokenClaims claims, CancellationToken cancellationToken) =>
        claims.Stamp is not string stamp
        || await cache.GetOrCreateAsync(
            CacheKey(claims.SubmissionId, stamp),
            token => new ValueTask<bool>(CheckAsync(claims.SubmissionId, stamp, token)),
            _cacheOptions,
            cancellationToken: cancellationToken);

    internal static string CacheKey(long submissionId, string stamp) =>
        $"audience:link_access:submission:{submissionId}:stamp:{stamp}";

    private async Task<bool> CheckAsync(long submissionId, string stamp, CancellationToken cancellationToken)
    {
        if (!AudienceLink.TryReadAccessStamp(stamp, out var linkId))
        {
            return false;
        }

        var link = await db.Links.AsNoTracking()
            .IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .FirstOrDefaultAsync(item => item.Id == linkId && item.SubmissionId == submissionId, cancellationToken);
        if (link?.AccessStamp() != stamp)
        {
            return false;
        }

        var membership = db.Memberships
            .IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .Where(item => item.Id == link.MembershipId && item.TenantId == link.TenantId);
        return await gates.PersonAsync(membership, cancellationToken) is not null;
    }
}
