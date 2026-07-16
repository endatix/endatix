using System.Text.Json.Serialization;

namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// How export output is serialized.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExportDeliveryFormat
{
    Csv = 0,
    Json = 1,
}
