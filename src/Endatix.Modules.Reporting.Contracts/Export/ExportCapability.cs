namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// A supported export combination derived from target, delivery format, and profile.
/// </summary>
public sealed record ExportCapability(
    ExportTarget Target,
    ExportDeliveryFormat DeliveryFormat,
    ExportProfile Profile,
    string WireKey,
    string Label,
    string ItemTypeName,
    string Description,
    ExportRequestFilterKind AllowedFilters);

/// <summary>
/// Export capability exposed to admin APIs and Hub.
/// </summary>
public sealed record ExportCapabilityDto(
    ExportTarget Target,
    ExportDeliveryFormat DeliveryFormat,
    ExportProfile Profile,
    string WireKey,
    string Label,
    string ItemTypeName,
    string Description,
    IReadOnlyList<string> AllowedFilters);
