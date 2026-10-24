namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Create args for <see cref="PropertyValue"/>. <see cref="IsTest"/> is the membership's, which never changes.
/// </summary>
public sealed record PropertyValueCreateArgs(
    long TenantId,
    long MembershipId,
    long PropertyId,
    string Value,
    bool IsTest);
