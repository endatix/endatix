namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Create args for <see cref="PropertyValue"/>. <see cref="IsTest"/> is the membership's, which never changes; build
/// the args with <see cref="For"/> so the cell cannot disagree with it.
/// </summary>
public sealed record PropertyValueCreateArgs(
    long TenantId,
    long MembershipId,
    long PropertyId,
    string Value,
    bool IsTest)
{
    /// <summary>A cell of <paramref name="membership"/>, which carries its tenant, id and test flag.</summary>
    public static PropertyValueCreateArgs For(Membership membership, long propertyId, string value) =>
        new(membership.TenantId, membership.Id, propertyId, value, membership.IsTest);
}
