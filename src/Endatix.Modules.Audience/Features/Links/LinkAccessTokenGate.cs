using Endatix.Core.Abstractions.Submissions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// A link's access token holds while its link still names its submission with the same stamp and the person is
/// still on the form: a reissue or a removal refuses it within <see cref="_checkLifetime"/>. The reads lift the
/// tenant filter, so the cached answer is the same for every caller: a signed-in user of another tenant must not
/// cache a refusal for the participant. It also follows the submission token's after-completion rule, uncached, so a
/// completed response is final at once. Tokens without a stamp were not issued by a link (staff tools) and always
/// hold.
/// </summary>
internal sealed class LinkAccessTokenGate(IAudienceDbContext db, ISubmissionTokenService submissionTokens, HybridCache cache)
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
        || (await cache.GetOrCreateAsync(
                CacheKey(claims.SubmissionId, stamp),
                token => new ValueTask<bool>(CheckAsync(claims.SubmissionId, stamp, token)),
                _cacheOptions,
                cancellationToken: cancellationToken)
            && await submissionTokens.AcceptsTokenAsync(claims.SubmissionId, cancellationToken));

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
        return await FormParticipants.Of(db, membership).AnyAsync(cancellationToken);
    }
}
