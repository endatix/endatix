using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Infrastructure.Exporting.Transformers;

namespace Endatix.Infrastructure.Exporting.ColumnDefinitions;

/// <summary>
/// Represents an abstract base column definition for exporting data.
/// Extract, Pipe, Format: ExtractRawValue feeds a single pipeline of IValueTransformer.
/// </summary>
public abstract class ColumnDefinition<T> where T : class
{
    public string Name { get; }
    public string JsonPropertyName { get; set; } = string.Empty;

    private readonly List<IValueTransformer> _transformers = new();

    protected ColumnDefinition(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Adds a value transformer to the pipeline.
    /// </summary>
    public ColumnDefinition<T> AddTransformer(IValueTransformer transformer)
    {
        Guard.Against.Null(transformer);

        _transformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Sets the formatter (e.g. from ExportOptions). Appended as last transformer; skipped for GetValue (JSON).
    /// </summary>
    public ColumnDefinition<T> WithFormatter(Func<object?, string> formatter)
    {
        Guard.Against.Null(formatter);

        _transformers.Add(new DelegateTransformer(value => formatter(value)));
        return this;
    }

    /// <summary>
    /// Hook for subclasses: returns the initial value (e.g. JsonElement or property value).
    /// </summary>
    protected abstract object? ExtractRawValue(T row, JsonDocument? document);

    /// <summary>
    /// Gets the value for export. Runs pipeline excluding formatter. Use for JSON export.
    /// </summary>
    public object? GetValue(TransformationContext<T> context)
    {
        var value = ExtractRawValue(context.Row, context.JsonDoc);
        var count = _transformers.Count;
        for (var i = 0; i < count; i++)
        {
            value = _transformers[i].Transform(value, context);
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

    /// <inheritdoc />
    protected override object? ExtractRawValue(T row, JsonDocument? document) => _accessor(row);
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
    protected override object? ExtractRawValue(T row, JsonDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        if (document.RootElement.TryGetProperty(_jsonPath, out var element))
        {
            return element;
        }

        return null;
    }
}
