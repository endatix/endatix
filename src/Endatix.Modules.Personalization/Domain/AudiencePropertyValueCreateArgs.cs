namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// Create args for <see cref="AudiencePropertyValue"/>.
/// </summary>
public sealed record AudiencePropertyValueCreateArgs(
    long TenantId,
    long AudienceMembershipId,
    long AudiencePropertyId,
    string Value);
