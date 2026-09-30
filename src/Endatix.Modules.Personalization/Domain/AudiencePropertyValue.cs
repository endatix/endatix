using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// One property cell on a form membership.
/// </summary>
public sealed class AudiencePropertyValue : BaseEntity, IAggregateRoot, ITenantOwned
{
    private AudiencePropertyValue() { }

    public AudiencePropertyValue(
        long tenantId,
        long audienceMembershipId,
        long audiencePropertyId,
        string value)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(audienceMembershipId);
        Guard.Against.NegativeOrZero(audiencePropertyId);
        Guard.Against.Null(value);

        TenantId = tenantId;
        AudienceMembershipId = audienceMembershipId;
        AudiencePropertyId = audiencePropertyId;
        Value = value;
    }

    public long TenantId { get; private set; }

    public long AudienceMembershipId { get; private set; }

    public long AudiencePropertyId { get; private set; }

    /// <summary>Canonical string. Multiple choice is a JSON array of choice keys.</summary>
    public string Value { get; private set; } = null!;

    public void SetValue(string value)
    {
        Guard.Against.Null(value);
        Value = value;
    }
}
