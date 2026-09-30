using Endatix.Modules.Personalization.Contracts;

namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// Create args for <see cref="AudienceProperty"/>.
/// </summary>
public sealed record AudiencePropertyCreateArgs(
    long TenantId,
    long FormId,
    string Name,
    string DataType,
    int SortOrder,
    string? ChoicesJson = null,
    bool AllowsOther = false);
