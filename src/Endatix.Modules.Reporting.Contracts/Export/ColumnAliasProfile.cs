namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Export-time column alias profile for CSV/JSON headers and property names.
/// </summary>
public enum ColumnAliasProfile
{
    /// <summary>
    /// Canonical storage keys are used as export keys.
    /// </summary>
    Native = 0,

    /// <summary>
    /// Crunch-safe sequential aliases (Q1, Q1_1, Q2_3, …).
    /// </summary>
    Crunch = 1,
}
