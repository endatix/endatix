namespace Endatix.Modules.Reporting.Contracts.Export;

using System.Text.Json.Serialization;

/// <summary>
/// Export-time column naming strategy for submissions CSV/JSON headers.
/// </summary>
[JsonConverter(typeof(ColumnAliasProfileJsonConverter))]
public enum ColumnAliasProfile
{
    /// <summary>
    /// Canonical storage keys are used as export keys.
    /// </summary>
    Native = 0,

    /// <summary>
    /// Sequential question-index aliases (Q1, Q1_1, Q2_3, …).
    /// </summary>
    Crunch = 1,
}
