using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Infrastructure.Exporting.ColumnDefinitions;

/// <summary>
/// Represents an abstract base column definition for exporting data.
/// Extract, Pipe, Format: ExtractRawValue feeds a single pipeline of IValueTransformer.
/// </summary>
public abstract class ColumnDefinition<T> where T : class
{
    public string Name { get; }
    public string JsonPropertyName { get; set; } = string.Empty;

    private readonly List<IValueTransformer> _pipeline = new();
    private int _formatterIndex = -1;

    protected ColumnDefinition(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Adds a value transformer to the pipeline.
    /// </summary>
    public ColumnDefinition<T> AddTransformer(IValueTransformer transformer)
    {
        _pipeline.Add(transformer ?? throw new ArgumentNullException(nameof(transformer)));
        return this;
    }

    /// <summary>
    /// Sets the formatter (e.g. from ExportOptions). Appended as last transformer; skipped for GetValue (JSON).
    /// </summary>
    public ColumnDefinition<T> WithFormatter(Func<object?, string> formatter)
    {
        ReplaceFormatter(new Transformers.FormatTransformer(formatter ?? throw new ArgumentNullException(nameof(formatter))));
        return this;
    }

    /// <summary>
    /// Hook for subclasses: returns the initial value (e.g. JsonElement or property value).
    /// </summary>
    protected abstract object? ExtractRawValue(TransformationContext<T> context);

    /// <summary>
    /// Gets the value for export. Runs pipeline excluding formatter. Use for JSON export.
    /// </summary>
    public object? GetValue(TransformationContext<T> context)
    {
        var value = ExtractRawValue(context);
        var endIndex = _formatterIndex >= 0 ? _formatterIndex - 1 : _pipeline.Count - 1;
        for (var i = 0; i <= endIndex && i < _pipeline.Count; i++)
        {
            var result = _pipeline[i].Transform(value, context);
            if (result is not null)
            {
                value = result;
            }
        }

        return value;
    }

    /// <summary>
    /// Gets the formatted value for CSV. Runs full pipeline (formatter produces string).
    /// </summary>
    public string GetFormattedValue(TransformationContext<T> context)
    {
        var value = GetValue(context);
        if (_formatterIndex >= 0)
        {
            return (string)_pipeline[_formatterIndex].Transform(value, context)!;
        }

        return FormatValue(value);
    }

    private void ReplaceFormatter(IValueTransformer formatter)
    {
        if (_formatterIndex >= 0)
        {
            _pipeline.RemoveAt(_formatterIndex);
            _formatterIndex = -1;
        }
        _pipeline.Add(formatter);
        _formatterIndex = _pipeline.Count - 1;
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

    /// <inheritdoc />
    protected override object? ExtractRawValue(TransformationContext<T> context) => _accessor(context.Row);
}

/// <summary>
/// Column definition for accessing JSON properties. Extracts JsonElement into the pipeline (zero-copy start).
/// </summary>
public sealed class JsonColumnDefinition<T> : ColumnDefinition<T> where T : class
{
    private readonly string _jsonPath;

    public JsonColumnDefinition(string name, string jsonPath) : base(name)
    {
        _jsonPath = jsonPath;
    }

    /// <inheritdoc />
    protected override object? ExtractRawValue(TransformationContext<T> context)
    {
        if (context.JsonDoc is null)
        {
            return null;
        }

        if (context.JsonDoc.RootElement.TryGetProperty(_jsonPath, out var element))
        {
            return element;
        }

        return null;
    }
}
