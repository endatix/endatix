using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Domain;

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
