using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Microsoft.Extensions.Logging;

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

    private readonly List<IValueTransformer> _transformers = new();
    private int _formatterIndex = -1;

    /// <summary>Exposed for derived classes to log transform failures.</summary>
    protected ILogger? Logger { get; private set; }

    protected ColumnDefinition(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Adds a value transformer to the pipeline. Return null when no change.
    /// </summary>
    public ColumnDefinition<T> AddTransformer(IValueTransformer transformer)
    {
        _transformers.Add(transformer ?? throw new ArgumentNullException(nameof(transformer)));
        return this;
    }

    /// <summary>
    /// Sets the formatter for presentation (e.g. from ExportOptions). Appended as last transformer; skipped for GetValue (JSON).
    /// </summary>
    public ColumnDefinition<T> WithFormatter(Func<object?, string> formatter)
    {
        ReplaceFormatter(new Transformers.FormatTransformer(formatter ?? throw new ArgumentNullException(nameof(formatter))));
        return this;
    }

    /// <summary>
    /// Sets the formatter as an IValueTransformer. Appended as last transformer; skipped for GetValue (JSON).
    /// </summary>
    public ColumnDefinition<T> WithFormatter(IValueTransformer formatter)
    {
        ReplaceFormatter(formatter ?? throw new ArgumentNullException(nameof(formatter)));
        return this;
    }

    /// <summary>
    /// Sets the logger used to log when a value transformation fails.
    /// </summary>
    public ColumnDefinition<T> WithLogger(ILogger logger)
    {
        Logger = logger;
        return this;
    }

    /// <summary>
    /// Extracts raw value and optional element. JsonColumnDefinition provides element for JSON transformers.
    /// </summary>
    protected abstract (object? value, JsonElement? element) GetRawValue(T row, JsonDocument? document);

    /// <summary>
    /// Applies value transformers to the given value. Excludes formatter (for JSON export).
    /// </summary>
    protected object? ApplyValueTransformers(object? value)
    {
        var endIndex = _formatterIndex >= 0 ? _formatterIndex - 1 : _transformers.Count - 1;
        for (var i = 0; i <= endIndex && i < _transformers.Count; i++)
        {
            try
            {
                var result = _transformers[i].Transform(value);
                if (result is not null)
                {
                    value = result;
                }
            }
            catch (Exception ex)
            {
                if (Logger is not null)
                {
                    Logger.LogError(ex, "Value transformation failed for column {ColumnName}", Name);
                }
            }
        }
        return value;
    }

    /// <summary>
    /// Gets the value for export: extracts, applies value transformers (excludes formatter). Use for JSON export.
    /// </summary>
    public virtual object? GetValue(T row, JsonDocument? document)
    {
        (var value, _) = GetRawValue(row, document);

        return ApplyValueTransformers(value);
    }

    /// <summary>
    /// Gets the formatted value for CSV: GetValue then formatter (or default FormatValue).
    /// </summary>
    public string GetFormattedValue(T row, JsonDocument? document)
    {
        var value = GetValue(row, document);

        if (_formatterIndex >= 0)
        {
            return (string)_transformers[_formatterIndex].Transform(value)!;
        }

        return FormatValue(value);
    }

    private void ReplaceFormatter(IValueTransformer formatter)
    {
        if (_formatterIndex >= 0)
        {
            _transformers.RemoveAt(_formatterIndex);
            _formatterIndex = -1;
        }
        _transformers.Add(formatter);
        _formatterIndex = _transformers.Count - 1;
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

    protected override (object? value, JsonElement? element) GetRawValue(T row, JsonDocument? document) =>
        (_accessor(row), null);
}

/// <summary>
/// Column definition for accessing JSON properties from a document.
/// Supports specialized IJsonValueTransformer for JsonElement (e.g. URL rewrite).
/// </summary>
public sealed class JsonColumnDefinition<T> : ColumnDefinition<T> where T : class
{
    private readonly string _jsonPropertyName;
    private readonly List<Core.Abstractions.Exporting.IJsonValueTransformer<T>> _jsonValueTransformers = new();

    public JsonColumnDefinition(string name, string jsonPropertyName) : base(name)
    {
        _jsonPropertyName = jsonPropertyName;
    }

    /// <summary>
    /// Adds a JSON-specific transformer (e.g. storage URL rewrite). Receives (JsonElement, row). Return null when no change.
    /// </summary>
    public JsonColumnDefinition<T> AddJsonTransformer(Core.Abstractions.Exporting.IJsonValueTransformer<T> transformer)
    {
        _jsonValueTransformers.Add(transformer ?? throw new ArgumentNullException(nameof(transformer)));
        return this;
    }

    /// <summary>
    /// Gets the raw value and element from the document.
    /// returns (value, element) where value is the raw value and element is the JSON element.
    /// </summary>
    protected override (object? value, JsonElement? element) GetRawValue(T row, JsonDocument? document)
    {
        if (document is null)
        {
            return (null, null);
        }

        if (document.RootElement.TryGetProperty(_jsonPropertyName, out var element))
        {
            object? value = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
            return (value, element);
        }

        return (null, null);
    }

    /// <inheritdoc />
    public override object? GetValue(T row, JsonDocument? document)
    {
        (var value, var element) = GetRawValue(row, document);

        if (element is { } jsonElement)
        {
            foreach (var jsonTransformer in _jsonValueTransformers)
            {
                try
                {
                    var result = jsonTransformer.Transform(jsonElement, row);
                    if (result is not null)
                    {
                        value = result;
                    }
                }
                catch (Exception ex)
                {
                    if (Logger is not null)
                    {
                        Logger.LogError(ex, "Value transformation failed for column {ColumnName}", Name);
                    }
                }
            }
        }

        return ApplyValueTransformers(value);
    }
}
