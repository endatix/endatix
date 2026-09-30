using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// Places an <see cref="AudienceMember"/> on one form. Deleting this row removes the person from
/// that form only.
/// </summary>
public sealed class AudienceMembership : BaseEntity, IAggregateRoot, ITenantOwned
{
    private AudienceMembership() { }

    public AudienceMembership(long tenantId, long formId, long audienceMemberId)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(audienceMemberId);

        TenantId = tenantId;
        FormId = formId;
        AudienceMemberId = audienceMemberId;
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    public long AudienceMemberId { get; private set; }
}
