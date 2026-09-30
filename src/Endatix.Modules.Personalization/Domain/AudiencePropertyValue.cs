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

    public AudiencePropertyValue(AudiencePropertyValueCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NegativeOrZero(args.AudienceMembershipId);
        Guard.Against.NegativeOrZero(args.AudiencePropertyId);
        Guard.Against.Null(args.Value);

        TenantId = args.TenantId;
        AudienceMembershipId = args.AudienceMembershipId;
        AudiencePropertyId = args.AudiencePropertyId;
        Value = args.Value;
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
