using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>Per-format numeric replacements for enabled special choices. Omitted codes keep the format defaults.</summary>
[Experimental("ENDATIX0006")]
public sealed record SpecialChoiceCodes(
    int? None = null,
    int? Refused = null,
    [property: JsonPropertyName("dontknow")] int? DontKnow = null)
{
    /// <summary>When omitted, Other retains its ordinary format-specific representation.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Other { get; init; }
}
