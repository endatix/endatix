namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Naming convention catalog entry exposed to admin APIs and Hub.
/// </summary>
public sealed record ColumnAliasNamingConventionDto(
    string WireKey,
    string Label,
    string Description,
    string? Example);
