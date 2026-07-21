using System.Text.Json;
using System.Text.Json.Serialization;

namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Request-time filter for submission completion when exporting from the reporting read model.
/// Omitted / null on the wire means <see cref="All"/> (backward compatible).
/// Wire: <c>all</c> | <c>completed</c> | <c>incomplete</c>.
/// </summary>
[JsonConverter(typeof(ExportCompletionStatusJsonConverter))]
public enum ExportCompletionStatus
{
    All = 0,
    Completed = 1,
    Incomplete = 2,
}

/// <summary>
/// Serializes <see cref="ExportCompletionStatus"/> as camelCase wire strings.
/// </summary>
public sealed class ExportCompletionStatusJsonConverter : JsonStringEnumConverter
{
    public ExportCompletionStatusJsonConverter()
        : base(JsonNamingPolicy.CamelCase)
    {
    }
}
