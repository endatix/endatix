namespace Endatix.Core.Entities;

/// <summary>
/// Represents a generic row for dynamic exports where the shape is not known at compile time.
/// </summary>
public sealed class DynamicExportRow
{
    /// <summary>
    /// Gets or sets the raw data of the row, typically a JSON string.
    /// </summary>
    public string Data { get; init; } = string.Empty;
}

