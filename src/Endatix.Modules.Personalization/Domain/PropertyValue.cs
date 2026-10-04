using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// One property cell on a form membership.
/// </summary>
public sealed class PropertyValue : BaseEntity, IAggregateRoot, ITenantOwned
{
    private PropertyValue() { }

    public PropertyValue(PropertyValueCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NegativeOrZero(args.MembershipId);
        Guard.Against.NegativeOrZero(args.PropertyId);
        Guard.Against.Null(args.Value);

        TenantId = args.TenantId;
        MembershipId = args.MembershipId;
        PropertyId = args.PropertyId;
        Value = args.Value;
    }

    public long TenantId { get; private set; }

    public long MembershipId { get; private set; }

    public long PropertyId { get; private set; }

    /// <summary>Canonical string. Multiple choice is a JSON array of choice keys.</summary>
    public string Value { get; private set; } = null!;

    public void SetValue(string value)
    {
        Guard.Against.Null(value);
        Value = value;
    }
}
