namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Create args for <see cref="PropertyValue"/>.
/// </summary>
public sealed record PropertyValueCreateArgs(
    long TenantId,
    long MembershipId,
    long PropertyId,
    string Value);
