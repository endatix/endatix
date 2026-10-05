using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Places an <see cref="Member"/> on one form. Deleting this row removes the person from
/// that form only.
/// </summary>
public sealed class Membership : BaseEntity, IAggregateRoot, ITenantOwned
{
    /// <summary>
    /// Database names of the unique indexes on <see cref="Membership"/>.
    /// </summary>
    public static class UniqueConstraints
    {
        public const string MemberPerForm = "IX_Memberships_Member";
    }

    private Membership() { }

    public Membership(long tenantId, long formId, long memberId)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(memberId);

        TenantId = tenantId;
        FormId = formId;
        MemberId = memberId;
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    public long MemberId { get; private set; }
}
