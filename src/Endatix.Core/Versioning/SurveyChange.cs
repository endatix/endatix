using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Endatix.Core.Versioning;

/// <summary>A display change from the live survey to the draft. Anchors identify the preceding
/// keyed sibling at this step; null denotes the start of the array.</summary>
[Experimental("ENDATIX0007")]
public sealed record SurveyChange(string Kind, string Path, JsonElement? Before = null, JsonElement? After = null,
    string? PredecessorBefore = null, string? PredecessorAfter = null);
