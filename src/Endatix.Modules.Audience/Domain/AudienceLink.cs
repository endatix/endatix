using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Hashed personalised link for one membership. Opening it creates the submission once.
/// </summary>
public sealed class AudienceLink : BaseEntity, ITenantOwned
{
    public const int TokenHashLength = 64;

    private const int AccessDigestLength = 16;

    /// <summary>Database names of the unique indexes on <see cref="AudienceLink"/>.</summary>
    public static class UniqueConstraints
    {
        public const string OnePerMembership = "IX_Links_Membership";
    }

    private AudienceLink()
    {
        TokenHash = string.Empty;
    }

    private AudienceLink(long tenantId, long formId, long membershipId, string tokenHash)
    {
        TenantId = tenantId;
        FormId = formId;
        MembershipId = membershipId;
        TokenHash = tokenHash;
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    public long MembershipId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTime? OpenedAt { get; private set; }

    public long? SubmissionId { get; private set; }

    public static (AudienceLink Link, string Token) Issue(long tenantId, long formId, long membershipId)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(membershipId);
        string token = NewToken();
        return (new AudienceLink(tenantId, formId, membershipId, Hash(token)), token);
    }

    /// <summary>
    /// A new token for the same link: the old token no longer opens it, and the new <see cref="AccessStamp"/> refuses
    /// the access tokens it handed out. An opened link keeps its submission, so the person resumes their response.
    /// </summary>
    public string Reissue()
    {
        string token = NewToken();
        TokenHash = Hash(token);
        return token;
    }

    /// <summary>
    /// What the link's access tokens are bound to: the link's id, then a digest of <see cref="TokenHash"/>, in
    /// lowercase hex. The digest changes with the hash, so a reissued link's old tokens stop opening its submission,
    /// and a token never carries the hash itself.
    /// </summary>
    public string AccessStamp() => $"{Id:x}{Hash($"access-stamp:{TokenHash}")[..AccessDigestLength]}";

    /// <summary>The link id an <see cref="AccessStamp"/> names; <see langword="false"/> for any other value.</summary>
    public static bool TryReadAccessStamp(string stamp, out long linkId)
    {
        linkId = 0;
        return stamp.Length > AccessDigestLength
            && long.TryParse(stamp[..^AccessDigestLength], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out linkId);
    }

    public static string Hash(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}
