namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Create args for <see cref="PropertyValue"/>, built only from the cell's membership (<see cref="For"/>), so a cell
/// carries its participant's tenant and test flag. The database refuses a cell that disagrees with its membership
/// (<c>FK_PropertyValues_Membership</c>).
/// </summary>
public sealed record PropertyValueCreateArgs
{
    private PropertyValueCreateArgs(Membership membership, long propertyId, string value)
    {
        TenantId = membership.TenantId;
        MembershipId = membership.Id;
        PropertyId = propertyId;
        Value = value;
        IsTest = membership.IsTest;
    }

    /// <summary>The membership's tenant.</summary>
    public long TenantId { get; }

    /// <summary>The membership the cell belongs to.</summary>
    public long MembershipId { get; }

    /// <summary>The Property the cell holds a value for.</summary>
    public long PropertyId { get; }

    /// <summary>The canonical value; <see cref="PropertyValue"/> refuses an empty one.</summary>
    public string Value { get; }

    /// <summary>The membership's <see cref="Membership.IsTest"/>, which never changes.</summary>
    public bool IsTest { get; }

    /// <summary>A cell of <paramref name="membership"/> for one Property.</summary>
    public static PropertyValueCreateArgs For(Membership membership, long propertyId, string value) =>
        new(membership, propertyId, value);
}
