using System.Text.Json.Serialization;

namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// What entity is exported.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExportTarget
{
    Submissions = 0,
    Codebook = 1,
}
