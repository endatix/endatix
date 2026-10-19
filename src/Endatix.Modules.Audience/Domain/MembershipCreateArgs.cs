namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Create args for <see cref="Membership"/>.
/// </summary>
public sealed record MembershipCreateArgs(
    long TenantId,
    long FormId,
    long MemberId,
    bool IsTest = false);
