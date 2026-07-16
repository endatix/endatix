using System.Text.Json.Serialization;

namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Integration projection profile for codebook exports.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExportProfile
{
    Native = 0,
    Shoji = 1,
}
