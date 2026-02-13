using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Context passed through the export pipeline. Provides row, JSON document, and logger.
/// </summary>
public readonly struct TransformationContext<T>
{
    public T Row { get; }
    public JsonDocument? JsonDoc { get; }
    public ILogger? Logger { get; }

    public TransformationContext(T row, JsonDocument? jsonDoc, ILogger? logger)
    {
        Row = row;
        JsonDoc = jsonDoc;
        Logger = logger;
    }
}
