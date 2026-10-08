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
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return (new AudienceLink(tenantId, formId, membershipId, Hash(token)), token);
    }

    public static string Hash(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void MarkOpened(long submissionId, DateTime openedAt)
    {
        Guard.Against.NegativeOrZero(submissionId);
        if (SubmissionId is not null)
        {
            return;
        }

        SubmissionId = submissionId;
        OpenedAt = openedAt;
    }
}
