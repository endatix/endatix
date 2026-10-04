using Endatix.Modules.Personalization.Contracts;

namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// Create args for <see cref="Property"/>.
/// </summary>
public sealed record PropertyCreateArgs(
    long TenantId,
    long FormId,
    string Name,
    string DataType,
    int SortOrder,
    string? ChoicesJson = null,
    bool AllowsOther = false);
