using System.Text.Json;

namespace Endatix.Infrastructure.Exporting.ColumnDefinitions;

/// <summary>
/// Represents an abstract base column definition for exporting data.
/// The type T is the type of the row to be exported.
/// </summary>
public abstract class ColumnDefinition<T> where T : class
{
    /// <summary>
    /// The name of the column as it will appear in the header.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The JSON property name (typically camelCase) for JSON exports.
    /// Pre-calculated during column initialization for performance.
    /// </summary>
    public string JsonPropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Optional function to transform the value for presentation.
    /// </summary>
    public Func<object?, string>? Transformer { get; private set; }

    protected ColumnDefinition(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Sets a transformer function for this column.
    /// </summary>
    public ColumnDefinition<T> WithTransformer(Func<object?, string> transformer)
    {
        Transformer = transformer;
        return this;
    }

    /// <summary>
    /// Extracts the raw value from a row and optional JSON document.
    /// </summary>
    public abstract object? ExtractValue(T row, JsonDocument? document);

    /// <summary>
    /// Gets the formatted value for a row, handling both extraction and formatting.
    /// </summary>
    public string GetFormattedValue(T row, JsonDocument? document)
    {
        var value = ExtractValue(row, document);

        if (Transformer != null)
        {
            return Transformer(value);
        }

        return FormatValue(value);
    }

    /// <summary>
    /// Default formatting for common value types.
    /// </summary>
    protected static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("o"),
            bool boolean => boolean.ToString().ToLowerInvariant(),
            _ => value.ToString() ?? string.Empty
        };
    }
}

/// <summary>
/// Column definition for accessing static properties of an entity.
/// </summary>
public sealed class StaticColumnDefinition<T> : ColumnDefinition<T> where T : class
{
    private readonly Func<T, object?> _accessor;

    public StaticColumnDefinition(string name, Func<T, object?> accessor) : base(name)
    {
        _accessor = accessor;
    }

    public override object? ExtractValue(T row, JsonDocument? _) => _accessor(row);
}

/// <summary>
/// Column definition for accessing JSON properties from a document.
/// </summary>
public sealed class JsonColumnDefinition<T> : ColumnDefinition<T> where T : class
{
    private readonly string _jsonPropertyName;

    public JsonColumnDefinition(string name, string jsonPropertyName) : base(name)
    {
        _jsonPropertyName = jsonPropertyName;
    }

    public override object? ExtractValue(T row, JsonDocument? document)
    {
        if (document == null)
        {
            return null;
        }

        if (document.RootElement.TryGetProperty(_jsonPropertyName, out var element))
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
        }

        return null;
    }
}
